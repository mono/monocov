using System;
using System.Collections;
using System.Xml;
using System.IO;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.CompilerServices.SymbolWriter;
using Mono.Cecil;
using Mono.Cecil.Metadata;

namespace MonoCov
{

public delegate void CoverageProgress (string item, double percent);

public class CoverageModel : CoverageItem {

	private string dataFileName;
	private Hashtable namespaces;
	private Hashtable classes;
	private Hashtable sources;

	private Hashtable loadedAssemblies;
	private Hashtable symbolFiles;

	public event CoverageProgress Progress;

	/**
	 * List of filters, which are strings
	 */
	private ArrayList filters;

	public CoverageModel ()
	{
		dataFileName = string.Empty;
		namespaces = new Hashtable ();
		classes = new Hashtable ();
		sources = new Hashtable ();
		filters = new ArrayList ();
		Progress += delegate {}; // better than having to check every time...
	}

	public Hashtable Classes {
		get {
			return classes;
		}
	}

	public Hashtable Namespaces {
		get {
			return namespaces;
		}
	}

	public void AddFilter (String pattern) {
		filters.Add (pattern);
	}

	private bool IsFiltered (string name)
	{

		// Check positive filters first
		bool hasPositive = false;
		bool found = false;
		foreach (String pattern in filters) {
			if (pattern [0] == '+') {
				string p = pattern.Substring (1);
				if (name.IndexOf (p) != -1) {
					//Console.WriteLine ("FILTERED: " + pattern + " -> " + name);
					found = true;
				}
				hasPositive = true;
			}
		}
		if (hasPositive && !found)
			return true;

		foreach (String pattern in filters) {
			if (pattern [0] == '-') {
				string p = pattern.Substring (1);
				if (name.IndexOf (p) != -1) {
					//Console.WriteLine ("FILTERED: " + pattern + " -> " + name);
					return true;
				}
			}
		}
		return false;
	}

	private void LoadAssemblies (XmlDocument dom)
	{
		foreach (XmlNode n in dom.GetElementsByTagName ("assembly")) {
			string assemblyName = n.Attributes ["name"].Value;
			string guid = n.Attributes ["guid"].Value;
			string filename = n.Attributes ["filename"].Value;
			MonoSymbolFile symbolFile;

			if (!File.Exists (filename)) {
				string newFilename = Path.Combine(Path.GetDirectoryName (dataFileName), Path.GetFileName (filename));
				if (File.Exists (newFilename))
					filename = newFilename;
			}

#if USE_REFLECTION
			Assembly assembly = Assembly.Load (assemblyName);

			MethodInfo getguid = typeof (Module).GetMethod (
					"Mono_GetGuid", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
					null, CallingConventions.Any, new Type [0], null);

			if (getguid != null) {
				Guid assembly_guid = (Guid)getguid.Invoke (assembly.GetLoadedModules ()[0], new object [0]);
				Console.WriteLine (assembly_guid);
				if (assembly_guid != new Guid (guid)) {
					Console.WriteLine ("WARNING: Loaded version of assembly " + assembly + " is different from the version used to collect coverage data.");
				}
			} else {
				Console.WriteLine ("WARNING: Can't verify the guid of " + assembly);
			}

			loadedAssemblies [assemblyName] = assembly;

			Console.Write ("Reading symbols for " + assembly + " ...");
			symbolFile = MonoSymbolFile.ReadSymbolFile (assembly);
			if (symbolFile == null)
				Console.WriteLine (" (No symbols found)");
			else {
				symbolFiles [assembly] = symbolFile;
				Console.WriteLine (" (" + symbolFile.SourceCount + " files, " + symbolFile.MethodCount + " methods)");
			}
#else
			AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly (filename);
			ModuleDefinition module = assembly.MainModule;
			if (module.Mvid != new Guid (guid)) {
				Console.WriteLine ("WARNING: Loaded version of assembly " + assembly + " is different from the version used to collect coverage data.");
			}
			loadedAssemblies [assemblyName] = assembly;

			Console.Write ("Reading symbols for " + assemblyName + " ...");
			symbolFile = MonoSymbolFile.ReadSymbolFile (filename + ".mdb");
			if (symbolFile == null)
				Console.WriteLine (" (No symbols found)");
			else {
				symbolFiles [assembly] = symbolFile;
				Console.WriteLine (" (" + symbolFile.SourceCount + " files, " + symbolFile.MethodCount + " methods)");
			}
#endif
		}
	}		

	private void LoadFilters (XmlDocument dom)
	{
		foreach (XmlNode n in dom.GetElementsByTagName ("filter")) {
			AddFilter (n.Attributes ["pattern"].Value);
		}
	}

#if USE_REFLECTION
	static Type LoadType (Assembly assembly, string name) {
		Type type = assembly.GetType (name);
		if (type != null)
			return type;
		int last_dot = name.LastIndexOf ('.');
		// covert names from IL to reflection naming
		// needed to deal with nested types
		while (last_dot >= 0) {
			StringBuilder sb = new StringBuilder (name);
			sb [last_dot] = '/';
			name = sb.ToString ();
			type = assembly.GetType (name);
			if (type != null)
				return type;
			last_dot = name.LastIndexOf ('.');
		}
		return null;
	}
#else
	static TypeDefinition LoadType (AssemblyDefinition assembly, string name) {
		TypeDefinition type = assembly.MainModule.GetType(name);
		if (type != null)
			return type;
		int last_dot = name.LastIndexOf ('.');
		// covert names from IL to reflection naming
		// needed to deal with nested types
		while (last_dot >= 0) {
			StringBuilder sb = new StringBuilder (name);
			sb [last_dot] = '/';
			name = sb.ToString ();
			type = assembly.MainModule.GetType(name);
			if (type != null)
				return type;
			last_dot = name.LastIndexOf ('.');
		}
		return null;
	}
#endif

	public void ReadFromFile (string fileName)
	{
		dataFileName = fileName;
		namespaces = new Hashtable ();
		classes = new Hashtable ();

		long begin = DateTime.Now.Ticks / 10000;
		long msec = DateTime.Now.Ticks / 10000;
		long msec2;

		loadedAssemblies = new Hashtable ();
		symbolFiles = new Hashtable ();

		XmlDocument dom = new XmlDocument ();
		Progress ("XML reading", 0);
		Console.Write ("Loading " + fileName + "...");
		dom.Load (new XmlTextReader (new FileStream (fileName, FileMode.Open)));
		Console.WriteLine (" Done.");

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("XML Reading: " + (msec2 - msec) + " msec");
		msec = msec2;

		Progress ("Load assemblies", 0.2);
		LoadAssemblies (dom);

		LoadFilters (dom);

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("Load assemblies: " + (msec2 - msec) + " msec");
		msec = msec2;

		Progress ("Load methods", 0.4);
		foreach (XmlNode n in dom.GetElementsByTagName ("method")) {
			string assemblyName = n.Attributes ["assembly"].Value;
			string className = n.Attributes ["class"].Value;
			string methodName = n.Attributes ["name"].Value;
			string token = n.Attributes ["token"].Value;
			if (n.FirstChild == null) {
				continue;
			}
			string cov_info = n.FirstChild.Value;
			int itok = int.Parse (token);
			
#if USE_REFLECTION
			Assembly assembly = (Assembly)loadedAssemblies [assemblyName];
			MonoSymbolFile symbolFile = (MonoSymbolFile)symbolFiles [assembly];

			if (symbolFile == null)
				continue;

			Type t = LoadType (assembly, className);
			if (t == null) {
				Console.WriteLine ("ERROR: Unable to resolve type " + className + " in " + assembly);
				continue;
			}

			ClassCoverageItem klass = ProcessClass (t);

			MethodEntry entry = symbolFile.GetMethodByToken (Int32.Parse (token));

			Module[] modules = assembly.GetModules();

			if (modules.Length > 1)
				Console.WriteLine("WARNING: Assembly had more than one module. Using the first.");

			Module module = modules[0];

			MethodBase monoMethod = module.ResolveMethod(Int32.Parse(token));

			ProcessMethod (monoMethod, entry, klass, methodName, cov_info);
#else
			if ((TokenType)(itok & 0xff000000) != TokenType.Method)
				continue;
			AssemblyDefinition assembly = (AssemblyDefinition)loadedAssemblies [assemblyName];
			MonoSymbolFile symbolFile = (MonoSymbolFile)symbolFiles [assembly];

			if (symbolFile == null)
				continue;

			TypeDefinition t = LoadType (assembly, className);
			if (t == null) {
				Console.WriteLine ("ERROR: Unable to resolve type " + className + " in " + assembly);
				continue;
			}

			ClassCoverageItem klass = ProcessClass (t);

			MethodEntry entry = symbolFile.GetMethodByToken (itok);

			MethodDefinition monoMethod = assembly.MainModule.LookupToken (
				new MetadataToken ((TokenType)(itok & 0xff000000), (uint)(itok & 0xffffff)))
				as MethodDefinition;
			//Console.WriteLine (monoMethod);
			ProcessMethod (monoMethod, entry, klass, methodName, cov_info);
#endif
		}

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("Process methods: " + (msec2 - msec) + " msec");
		msec = msec2;

		// Add info for klasses for which we have no coverage

#if USE_REFLECTION
		foreach (Assembly assembly in loadedAssemblies.Values) {
			foreach (Type t in assembly.GetTypes ()) {
				ProcessClass (t);
			}
		}

		// Add info for methods for which we have no coverage

		foreach (ClassCoverageItem klass in classes.Values) {
			foreach (MethodInfo mb in klass.type.GetMethods (BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
				MonoSymbolFile symbolFile = (MonoSymbolFile)symbolFiles [klass.type.Assembly];
				if (symbolFile == null)
					continue;

				if (! klass.methodsByMethod.ContainsKey (mb)) {
					MethodEntry entry = symbolFile.GetMethod (mb);
					ProcessMethod (mb, entry, klass, mb.Name, null);
				}
			}
		}
#else
		Progress ("Not covered classes", 0.6);
		foreach (AssemblyDefinition assembly in loadedAssemblies.Values) {
			foreach (TypeDefinition t in assembly.MainModule.Types) {
				ProcessClass (t);
			}
		}

		Progress ("Not covered methods", 0.7);
		// Add info for methods for which we have no coverage
		foreach (ClassCoverageItem klass in classes.Values) {
			foreach (MethodDefinition mb in klass.type.Methods) {
				MonoSymbolFile symbolFile = (MonoSymbolFile)symbolFiles [klass.type.Module.Assembly];
				if (symbolFile == null)
					continue;

				if (! klass.methodsByMethod.ContainsKey (mb)) {
					MethodEntry entry = symbolFile.GetMethodByToken ((int)mb.MetadataToken.ToUInt32());
					ProcessMethod (mb, entry, klass, mb.Name, null);
				}
			}
		}
#endif

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("Additional classes: " + (msec2 - msec) + " msec");
		msec = msec2;

		Progress ("Compute coverage", 0.9);
		// Compute coverage for all items

		computeCoverage (true);

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("Compute coverage: " + (msec2 - msec) + " msec");
		msec = msec2;

		Console.WriteLine ("All: " + (msec2 - begin) + " msec");
		Progress ("Done loading", 0.9);

		// Free memory
		symbolFiles = null;
	}

	//
	// Computes the coverage of METHOD
	//

	private char[] digits = "0123456789".ToCharArray ();
	private char[] ws = "\t\n ".ToCharArray ();

	private int parsePositiveInteger (string s, int pos) {
		int n = 0;

		while (s [pos] >= '0' && s [pos] <= '9'){
			n = n * 10 + (s [pos] - '0');
			pos ++;
		}
		return n;
	}

	private void computeMethodCoverage (MethodCoverageItem method, LineNumberEntry[] lines, string cov_info)
	{

		ClassCoverageItem klass = method.Class;
		SourceFileCoverageData source = klass.sourceFile;

		source.AddMethod (method);

		int nlines = method.endLine - method.startLine + 1;

		int[] coverage = new int [nlines];

		if (cov_info == null) {
			for (int i = 0; i < nlines; ++i)
				coverage [i] = 0;
		}
		else {
			for (int i = 0; i < nlines; ++i)
				coverage [i] = -1;

			// Hand crafted parsing code since this is performance critical
			int pos = 0;
			int prev_offset = 0;
			while (pos < cov_info.Length) {
				int pos2 = cov_info.IndexOfAny (digits, pos);
				if (pos2 == -1)
					break;
				pos = cov_info.IndexOfAny (ws, pos2);
				if (pos == -1)
					break;

				int offset = parsePositiveInteger (cov_info, pos2);

				pos2 = cov_info.IndexOfAny (digits, pos);
				if (pos2 == -1)
					break;
				pos = cov_info.IndexOfAny (ws, pos2);

				int count = parsePositiveInteger (cov_info, pos2);

				offset += prev_offset;
				prev_offset = offset;

				int line1 = 0;
				int line2 = 0;

				bool found = GetSourceRangeFor (offset, method, lines, ref line1, ref line2);

				/*
				  if (found && (entry.Name.IndexOf ("Find") != -1)) {
				  Console.WriteLine ("OFFSET: " + offset + " " + line1 + ":" + line2);
				  }
				*/

				if (found) {
					for (int i = line1; i < line2 + 1; ++i)
						if ((i >= method.startLine) && (i <= method.endLine))
							if (coverage [i - method.startLine] < count)
								coverage [i - method.startLine] = count;
				}
			}
		}

		int hit = 0;
		int missed = 0;

		for (int i = 0; i < nlines; ++i) {
			int count = coverage [i];
			if (count > 0)
				hit ++;
			else if (count == 0)
				missed ++;
		}

		method.setCoverage (hit, missed);

		method.lineCoverage = coverage;
	}

	//
	// Return a range of source lines which have something to do with OFFSET.
	//
	private bool GetSourceRangeFor (int offset, MethodCoverageItem method,
					   LineNumberEntry[] lines,
					   ref int startLine, ref int endLine)
	{
		for (int i = 0; i < lines.Length; ++i) {
			if (offset >= lines [i].Offset)
				if (i == lines.Length - 1) {
					startLine = lines [i].Row;
					endLine = lines [i].Row;
					return true;
				}
				else if (offset < lines [i + 1].Offset) {
					startLine = lines [i].Row;
					endLine = lines [i + 1].Row - 1;
					return true;
				}
		}

		if (offset <= lines [0].Offset) {
			return false;
		}
		else {
			for (int i = 0; i < lines.Length; ++i)
				Console.WriteLine (lines [i]);
			throw new Exception ("Unable to determine source range for offset " + offset + " in " + method.name);
		}
	}

#if USE_REFLECTION
	private ClassCoverageItem ProcessClass (Type t)
#else
	private ClassCoverageItem ProcessClass (TypeDefinition t)
#endif
	{
		string className = t.FullName;
		int nsindex = className.LastIndexOf (".");
		string namespace2;
		string scopedName;
		if (nsindex == -1) {
			namespace2 = "<GLOBAL>";
			scopedName = className;
		} else if (nsindex == 0) {
			namespace2 = "<GLOBAL>";
			scopedName = className.Substring (1);
		}
		else {
			namespace2 = className.Substring (0, nsindex);
			scopedName = className.Substring (nsindex + 1);
		}

		// Create namespaces
		NamespaceCoverageItem ns = (NamespaceCoverageItem)namespaces [namespace2];
		if (ns == null) {
			string nsPrefix = "";
			foreach (String nsPart in namespace2.Split ('.')) {
				if (nsPrefix == "")
					nsPrefix = nsPart;
				else
					nsPrefix = nsPrefix + "." + nsPart;
				NamespaceCoverageItem ns2 = (NamespaceCoverageItem)namespaces [nsPrefix];
				if (ns2 == null) {
					if (ns == null)
						ns2 = new NamespaceCoverageItem (this, nsPrefix);
					else
						ns2 = new NamespaceCoverageItem (ns, nsPrefix);
					namespaces [nsPrefix] = ns2;
				}
				ns = ns2;
			}					
		}

		ClassCoverageItem klass = (ClassCoverageItem)classes [className];
		if (klass == null) {
			klass = new ClassCoverageItem (ns);
			klass.name_space = namespace2;
			klass.name = scopedName;
			klass.type = t;
			klass.parent = ns;

#if USE_REFLECTION
			klass.filtered = IsFiltered ("[" + t.Assembly + "]" + className);
#else
			klass.filtered = IsFiltered ("[" + t.Module.Name + "]" + className);
#endif
			classes [className] = klass;
		}

		return klass;
	}

#if USE_REFLECTION
	private void ProcessMethod (MethodBase monoMethod, MethodEntry entry, ClassCoverageItem klass, string methodName, string cov_info)
#else
	private void ProcessMethod (MethodDefinition monoMethod, MethodEntry entry, ClassCoverageItem klass, string methodName, string cov_info)
#endif
	{
		if (entry == null)
			// Compiler generated, abstract method etc.
			return;

		LineNumberEntry[] lines = entry.GetLineNumberTable ().LineNumbers;

		if (lines.Length == 0)
			return;

		int start_line = lines [0].Row;
		int end_line = lines [lines.Length - 1].Row;

		MethodCoverageItem method 
			= new MethodCoverageItem (klass, methodName);

		method.startLine = start_line;
		method.endLine = end_line;
#if USE_REFLECTION
		method.filtered = IsFiltered ("[" + monoMethod.DeclaringType.Assembly + "]" + monoMethod.DeclaringType + "::" + monoMethod.Name);
#else
		method.filtered = IsFiltered ("[" + monoMethod.DeclaringType.Module.Name + "]" + monoMethod.DeclaringType + "::" + monoMethod.Name);
#endif
		klass.methodsByMethod [monoMethod] = method;


		if (klass.sourceFile == null) {
			string sourceFile = entry.CompileUnit.SourceFile.FileName;

			SourceFileCoverageData source = (SourceFileCoverageData)sources [sourceFile];
			if (source == null) {
				source = new SourceFileCoverageData (sourceFile);
				sources [sourceFile] = source;
			}
			klass.sourceFile = source;
		}
			
		computeMethodCoverage (method, lines, cov_info);
	}
}
}
