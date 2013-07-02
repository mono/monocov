
#include <stdio.h>
#include <string.h>
#include <errno.h>
#include <unistd.h>
#include <stdlib.h>
#include <glib.h>

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

/* Pointer hack: this should be accessible from the mono embedding api */

typedef struct _MonoMethodHack {
	guint16 flags;  /* method flags */
	guint16 iflags; /* method implementation flags */
	guint32 token;
	void *klass;
	void *signature;
	/* name is useful mostly for debugging */
	const char *name;
	/* this is used by the inlining algorithm */
	unsigned int inline_info:1;
	unsigned int inline_failure:1;
	unsigned int wrapper_type:5;
	unsigned int string_ctor:1;
	unsigned int save_lmf:1;
	unsigned int dynamic:1; /* created & destroyed during runtime */
	unsigned int sre_method:1; /* created at runtime using Reflection.Emit */
	unsigned int is_generic:1; /* whenever this is a generic method definition */
	unsigned int is_inflated:1; /* whether we're a MonoMethodInflated */
	unsigned int skip_visibility:1; /* whenever to skip JIT visibility checks */
	unsigned int verification_success:1; /* whether this method has been verified successfully.*/
	/* TODO we MUST get rid of this field, it's an ugly hack nobody is proud of. */
	unsigned int is_mb_open : 1;		/* This is the fully open instantiation of a generic method_builder. Worse than is_tb_open, but it's temporary */
	signed int slot : 16;

	/*
	 * If is_generic is TRUE, the generic_container is stored in image->property_hash, 
	 * using the key MONO_METHOD_PROP_GENERIC_CONTAINER.
	 */
} MonoMethodHack;

typedef struct _MonoMethodPInvoke {
	MonoMethodHack method;
	gpointer addr;
	/* add marshal info */
	guint16 piflags;  /* pinvoke flags */
	guint16 implmap_idx;  /* index into IMPLMAP */
} MonoMethodPInvokeHack;

typedef struct _MonoMethodInflated MonoMethodInflated;

struct _MonoMethodInflated {
	union {
		MonoMethodHack method;
		MonoMethodPInvokeHack pinvoke;
	} method;
	 
	MonoMethodHeader *header;
	MonoMethod *declaring;		/* the generic method definition. */
};

/* End Pointer hack: this should be accessible from the mono embedding api */


static char
*parse_generic_type_names(char *string);

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

		char buf [2048];
		while (fgets (buf, 2048, filterfile) != NULL) {
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
	/* we don't deal with unloading, so disable it for now */
	setenv ("MONO_NO_UNLOAD", "1", 1);
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

	spec = NULL; /* compile a pattern later */
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

static char
*parse_generic_type_names(char *name)
{
	char *new_name,*ret;
	int within_generic_declaration=0, generic_members=1;
	if( !(ret = new_name = calloc(strlen(name) * 4 + 1, sizeof(char))) )
		return NULL;
	
	do
	{
		switch(*name)
		{
			case '<':
				within_generic_declaration = 1;
				break;
			case '>':
				within_generic_declaration = 0;
				if( *(name-1) != '<')
				{
					*new_name++ = '`';
					*new_name++ = '0' + generic_members;
				}
				else
				{
					memcpy(new_name,"&lt;&gt;",8);
					new_name+=8;
				}
				generic_members = 0;
				break;
			case ',':
				generic_members++;
				break;
			default:
				if(!within_generic_declaration)
					*new_name++ = *name;
				break;
		}
	}while(*name++);
	return ret;
}

static void
output_method (MonoMethod *method, gpointer dummy, MonoProfiler *prof)
{
	MonoMethodHeader *header;
	char *classname;
	char *tmpsig;
	char *tmpname;
	FILE *outfile;
	MonoClass *klass;
	MonoImage *image;

	MonoMethod *covered_method;

    if(((MonoMethodHack *) method)->is_inflated)
    {
        MonoMethodInflated *imethod = (MonoMethodInflated *) method;
        covered_method = imethod->declaring;
    }
    else
    {
        covered_method = method;
    }

	
	outfile = prof->outfile;
	header = mono_method_get_header (covered_method);

	tmpsig = mono_signature_get_desc (mono_method_signature (covered_method), TRUE);
	tmpsig = g_markup_escape_text (tmpsig, strlen (tmpsig));

	klass = mono_method_get_class (covered_method);
	classname = parse_generic_type_names (mono_type_get_name (mono_class_get_type (klass)));
	image = mono_class_get_image (klass);

	tmpname = (char*)mono_method_get_name (covered_method);
	tmpname = g_markup_escape_text (tmpname, strlen (tmpname));

	fprintf (outfile, "\t<method assembly=\"%s\" class=\"%s\" name=\"%s (%s)\" token=\"%d\">\n",
			 mono_image_get_name (image),
			 classname, tmpname,
			 tmpsig, mono_method_get_token (covered_method));

	g_free (tmpsig);
	g_free (tmpname);
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
	fprintf (outfile, "<coverage version=\"%s\">\n", VERSION);

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
