
#include <stdio.h>
#include <string.h>
#include <errno.h>
#include <unistd.h>
#include <stdlib.h>

#include <mono/metadata/class.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/debug-helpers.h>
#include <mono/metadata/profiler.h>

struct _MonoProfiler {
	/* Contains the methods for which we have coverage data */
	GHashTable *methods;

    /* A list of classes for which we are collecting coverage data */
	GHashTable *classes;

    /* A list of assemblies for which we are collecting coverage data */
    GHashTable *assemblies;

    char *outfile_name;

    GPtrArray *filters;

    GPtrArray *filters_as_str;

    GHashTable *filtered_classes;

	FILE *outfile;
};

static void
add_filter (MonoProfiler *prof, const char *filter);

static void
assembly_load (MonoProfiler *prof, MonoAssembly *assembly, int result);

static void
coverage_shutdown (MonoProfiler *prof);

static gboolean
collect_coverage_for (MonoProfiler *prof, MonoMethod *method);

void
mono_profiler_startup (char *arg)
{
	gchar **ptr;
	char *filterfile_name = NULL;
	gchar **args;

	/* Why does the runtime passes the module name to us ? */
	if (strstr (arg, ":"))
		arg = strstr (arg, ":") + 1;
	else
		arg = NULL;
	args = g_strsplit (arg ? arg : "", ",", -1);

	MonoProfiler *prof = g_new0 (MonoProfiler, 1);

	prof->methods = g_hash_table_new (NULL, NULL);
	prof->classes = g_hash_table_new (NULL, NULL);
    prof->assemblies = g_hash_table_new (NULL, NULL);

	for (ptr = args; ptr && *ptr; ptr++) {
		const char *arg = *ptr;
		gchar *message;

		if (strncmp (arg, "filterfile=", 11) == 0)
			filterfile_name = g_strdup (arg + 11);
		else
			if (strncmp (arg, "outfile=", 8) == 0)
				prof->outfile_name = g_strdup (arg + 8);
		else
			if (strncmp (arg, "-", 1) == 0) {
				add_filter (prof, arg);
			}
			else if (strncmp (arg, "+", 1) == 0) {
				add_filter (prof, arg);
			}
			else {
				message = g_strdup_printf ("Unknown argument '%s'.", arg);
				fprintf (stderr, "monocov | Error while processing arguments: %s\n", message);
				g_free (message);
			}
	}

	g_strfreev (args);

	if (filterfile_name) {
		FILE *filterfile;

		filterfile = fopen (filterfile_name, "r");
		if (!filterfile) {
			fprintf (stderr, "coverage.c: Unable to open filter file '%s'.\n", filterfile_name);
			exit (1);
		}

		while (!feof (filterfile)) {
			char buf [2048];

			fgets (buf, 2048, filterfile);
			buf [sizeof (buf) - 1] = '\0';

			if ((buf [0] == '#') || (buf [0] == '\0'))
				continue;

			if (buf [strlen (buf) - 1] == '\n')
				buf [strlen (buf) - 1] = '\0';

			add_filter (prof, buf);
		}
		fclose (filterfile);
	}

	mono_profiler_install (prof, coverage_shutdown);
	mono_profiler_set_events (MONO_PROFILE_INS_COVERAGE | MONO_PROFILE_ASSEMBLY_EVENTS);
	mono_profiler_install_coverage_filter (collect_coverage_for);
	mono_profiler_install_assembly (NULL, assembly_load, NULL, NULL);
}

static void
assembly_load (MonoProfiler *prof, MonoAssembly *assembly, int result)
{
	/* Unfortunately, this doesn't get called... */
}

static gboolean
collect_coverage_for (MonoProfiler *prof, MonoMethod *method)
{
	int i;
	char *classname;
	char *fqn;
	MonoMethodHeader *header;
	gboolean has_positive, found;
	guint32 iflags, flags, code_size;
	MonoClass *klass;
	MonoImage *image;

	flags = mono_method_get_flags (method, &iflags);
	if ((iflags & 0x1000 /*METHOD_IMPL_ATTRIBUTE_INTERNAL_CALL*/) ||
	    (flags & 0x2000 /*METHOD_ATTRIBUTE_PINVOKE_IMPL*/))
		return FALSE;

	//if (method->wrapper_type != MONO_WRAPPER_NONE)
	//	return FALSE;

	klass = mono_method_get_class (method);
	image = mono_class_get_image (klass);
	/* Hacky way of determining the executing assembly */
	if (! prof->outfile_name && (strcmp (mono_method_get_name (method), "Main") == 0)) {
		prof->outfile_name = g_strdup_printf ("%s.cov", mono_image_get_filename (image));
	}

	/* Check filters */
	if (prof->filters) {
		/* Check already filtered classes first */
		if (g_hash_table_lookup (prof->filtered_classes, klass))
			return FALSE;

		classname = mono_type_get_name (mono_class_get_type (klass));

		fqn = g_strdup_printf ("[%s]%s", mono_image_get_name (image), classname);

		// Check positive filters first
		has_positive = FALSE;
		found = FALSE;
		for (i = 0; i < prof->filters->len; ++i) {
			char *filter = g_ptr_array_index (prof->filters_as_str, i);
			if (filter [0] == '+') {
				filter = &filter [1];
				if (strstr (fqn, filter) != NULL)
					found = TRUE;
				has_positive = TRUE;
			}
		}
		if (has_positive && !found)
			return FALSE;

		for (i = 0; i < prof->filters->len; ++i) {
			// Is substring search suffices ???
//			GPatternSpec *spec = g_ptr_array_index (filters, i);
//			if (g_pattern_match_string (spec, classname)) {
			char *filter = g_ptr_array_index (prof->filters_as_str, i);
			if (filter [0] == '+')
				continue;

			// Skip '-'
			filter = &filter [1];
			if (strstr (fqn, filter) != NULL) {
				g_hash_table_insert (prof->filtered_classes, klass, klass);
				return FALSE;
			}
		}
		g_free (fqn);
		g_free (classname);
	}

	header = mono_method_get_header (method);

	mono_method_header_get_code (header, &code_size, NULL);
	if (code_size > 20000) {
		exit (1);
		g_warning ("Unable to instrument method %s:%s since it is too complex.", mono_class_get_name (klass), mono_method_get_name (method));
		return FALSE;
	}

	g_hash_table_insert (prof->methods, method, method);

	g_hash_table_insert (prof->classes, klass, klass);

	g_hash_table_insert (prof->assemblies, mono_image_get_assembly (image), mono_image_get_assembly (image));

	return TRUE;
}

static void
add_filter (MonoProfiler *prof, const char *filter)
{
	GPatternSpec *spec;

	if (prof->filters == NULL) {
		prof->filters = g_ptr_array_new ();
		prof->filters_as_str = g_ptr_array_new ();
		prof->filtered_classes = g_hash_table_new (NULL, NULL);
	}

	g_ptr_array_add (prof->filters, spec);
	g_ptr_array_add (prof->filters_as_str, g_strdup (filter));
}

static void
output_filters (MonoProfiler *prof, FILE *outfile)
{
	int i;

	if (prof->filters) {
		for (i = 0; i < prof->filters_as_str->len; ++i) {
			char *str = g_ptr_array_index (prof->filters_as_str, i);

			fprintf (outfile, "\t<filter pattern=\"%s\"/>\n", 
					 g_markup_escape_text (str, strlen (str)));
		}
	}
}
	
static void
output_assembly (MonoAssembly *assembly, MonoAssembly *assembly2, FILE *outfile)
{
	MonoImage *image = mono_assembly_get_image (assembly);
	fprintf (outfile, "\t<assembly name=\"%s\" guid=\"%s\" filename=\"%s\"/>\n",
		mono_image_get_name (image), mono_image_get_guid (image), mono_image_get_filename (image));
}

static int count;
static int prev_offset;

static void
output_entry (MonoProfiler *prof, const MonoProfileCoverageEntry *entry)
{
	count ++;
	if ((count % 8) == 0)
		fprintf (prof->outfile, "\n\t\t");
	
	fprintf (prof->outfile, "%d %d\t", entry->iloffset - prev_offset, entry->counter);
	prev_offset = entry->iloffset;
}

static void
output_method (MonoMethod *method, gpointer dummy, MonoProfiler *prof)
{
	MonoMethodHeader *header;
	int i;
	char *classname;
	char *tmpsig;
	FILE *outfile;
	MonoClass *klass;
	MonoImage *image;

	outfile = prof->outfile;
	header = mono_method_get_header (method);

	tmpsig = mono_signature_get_desc (mono_method_signature (method), TRUE);
	tmpsig = g_markup_escape_text (tmpsig, strlen (tmpsig));

	klass = mono_method_get_class (method);
	classname = mono_type_get_name (mono_class_get_type (klass));
	image = mono_class_get_image (klass);

	fprintf (outfile, "\t<method assembly=\"%s\" class=\"%s\" name=\"%s (%s)\" token=\"%d\">\n",
			 mono_image_get_name (image),
			 classname, mono_method_get_name (method),
			 tmpsig, mono_method_get_token (method));

	g_free (tmpsig);
	fprintf (outfile, "\t\t");
	count = 0;
	prev_offset = 0;

	mono_profiler_coverage_get (prof, method, output_entry);

	fprintf (outfile, "\n");
	fprintf (outfile, "\t</method>\n");
}

static void
coverage_shutdown (MonoProfiler *prof)
{
	FILE *outfile;

	if (!prof->outfile_name)
		prof->outfile_name = g_strdup ("/dev/stdout");

	printf ("Dumping coverage data to %s ...\n", prof->outfile_name);

	outfile = fopen (prof->outfile_name, "w");
	if (!outfile) {
		fprintf (stderr, "coverage: unable to create result file %s: %s.\n",
				 prof->outfile_name, strerror (errno));
		return;
	}
	prof->outfile = outfile;

	fprintf (outfile, "<?xml version=\"1.0\"?>\n");
	fprintf (outfile, "<coverage>\n");

	/*
	 * The UI doesn't deal well with this enabled.
	 * output_filters (prof, outfile);
	 */

	g_hash_table_foreach (prof->assemblies, (GHFunc)output_assembly, outfile);

	g_hash_table_foreach (prof->methods, (GHFunc)output_method, prof);

	fprintf (outfile, "</coverage>\n");
	fclose (outfile);

	printf ("Done.\n");
}
