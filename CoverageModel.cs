
using System;
using System.Collections;
using System.Xml;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using Mono.CSharp.Debugger;

namespace MonoCov {

public class CoverageModel : CoverageItem {

	private Hashtable namespaces;
	private Hashtable classes;
	private Hashtable sources;

	private Hashtable loadedAssemblies;
	private Hashtable symbolFiles;

	/**
	 * List of filters, which are strings
	 */
	private ArrayList filters;

	public CoverageModel () {
		namespaces = new Hashtable ();
		classes = new Hashtable ();
		sources = new Hashtable ();
		filters = new ArrayList ();
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

	private bool IsFiltered (string name) {

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

	private void LoadAssemblies (XmlDocument dom) {
		foreach (XmlNode n in dom.GetElementsByTagName ("assembly")) {
			string assemblyName = n.Attributes ["name"].Value;
			string guid = n.Attributes ["guid"].Value;

			Assembly assembly = Assembly.Load (assemblyName);

			if  (assembly.GetLoadedModules ()[0].Mono_GetGuid () !=
				 new Guid (guid)) {
				Console.WriteLine ("WARNING: Loaded version of assembly " + assembly + " is different from the version used to collect coverage data.");
			}

			MonoSymbolFile symbolFile;

			loadedAssemblies [assemblyName] = assembly;

			Console.Write ("Reading symbols for " + assembly + " ...");
			symbolFile = MonoSymbolFile.ReadSymbolFile (assembly);
			if (symbolFile == null)
				Console.WriteLine (" (No symbols found)");
			else {
				symbolFiles [assembly] = symbolFile;
				Console.WriteLine (" (" + symbolFile.SourceCount + " files, " + symbolFile.MethodCount + " methods)");
			}
		}
	}		

	private void LoadFilters (XmlDocument dom) {
		foreach (XmlNode n in dom.GetElementsByTagName ("filter")) {
			AddFilter (n.Attributes ["pattern"].Value);
		}
	}

	public void ReadFromFile (string fileName) {
		namespaces = new Hashtable ();
		classes = new Hashtable ();

		long begin = DateTime.Now.Ticks / 10000;
		long msec = DateTime.Now.Ticks / 10000;
		long msec2;

		loadedAssemblies = new Hashtable ();
		symbolFiles = new Hashtable ();

		XmlDocument dom = new XmlDocument ();
		Console.Write ("Loading " + fileName + "...");
		dom.Load (new XmlTextReader (new FileStream (fileName, FileMode.Open)));
		Console.WriteLine (" Done.");

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("XML Reading: " + (msec2 - msec) + " msec");
		msec = msec2;

		LoadAssemblies (dom);

		LoadFilters (dom);

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("Load assemblies: " + (msec2 - msec) + " msec");
		msec = msec2;

		foreach (XmlNode n in dom.GetElementsByTagName ("method")) {
			string assemblyName = n.Attributes ["assembly"].Value;
			string className = n.Attributes ["class"].Value;
			string methodName = n.Attributes ["name"].Value;
			string token = n.Attributes ["token"].Value;
			string cov_info = n.FirstChild.Value;
			
			Assembly assembly = (Assembly)loadedAssemblies [assemblyName];
			MonoSymbolFile symbolFile = (MonoSymbolFile)symbolFiles [assembly];

			if (symbolFile == null)
				continue;

			Type t = assembly.GetType (className);
			if (t == null) {
				Console.WriteLine ("ERROR: Unable to resolve type " + className + " in " + assembly);
				return;
			}

			ClassCoverageItem klass = ProcessClass (t);

			MethodEntry entry = symbolFile.GetMethodByToken (Int32.Parse (token));

			MethodBase monoMethod = (MethodBase) typeof (Assembly).InvokeMember ("MonoDebugger_GetMethod",
				BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.NonPublic,
				null, assembly, new object [2] { assembly, Int32.Parse (token) }, null); 

			ProcessMethod (monoMethod, entry, klass, methodName, cov_info);
		}

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("Process methods: " + (msec2 - msec) + " msec");
		msec = msec2;

		// Add info for klasses for which we have no coverage

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

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("Additional classes: " + (msec2 - msec) + " msec");
		msec = msec2;

		// Compute coverage for all items

		computeCoverage (true);

		msec2 = DateTime.Now.Ticks / 10000;
		Console.WriteLine ("Compute coverage: " + (msec2 - msec) + " msec");
		msec = msec2;

		Console.WriteLine ("All: " + (msec2 - begin) + " msec");

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

	private void computeMethodCoverage (MethodCoverageItem method,
										LineNumberEntry[] lines,
										string cov_info) {

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
								   ref int startLine, ref int endLine) {

		/**
		 * The line number info generated by mcs is pretty strange sometimes... :)
		 */

		/**
		 * First, we split the range of IL offsets into consecutive blocks and
		 * identify the block which contains OFFSET. Then we identify the range
		 * of source lines which are mapped to this range by the line number 
		 * entries.
		 */

		int beginOffset = 0;
		int endOffset = 0;
		int i;

		for (i = 0; i < lines.Length; ++i) {
			if (offset >= lines [i].Offset)
				if (i == lines.Length - 1) {
					beginOffset = lines [i].Offset;
					endOffset = lines [i].Offset;
					break;
				}
				else if (offset < lines [i + 1].Offset) {
					beginOffset = lines [i].Offset;
					endOffset = lines [i + 1].Offset - 1;
					break;
				}
		}

		/*
		if (method.Name.IndexOf ("Find") != -1) {
			Console.WriteLine ("OFFSET: " + offset + " " + beginOffset + " " + endOffset);
		}
		*/

		if (i == lines.Length) {
			if (offset <= lines [0].Offset) {
				return false;
			}
			else {
				for (i = 0; i < lines.Length; ++i)
					Console.WriteLine (lines [i]);
				throw new Exception ("Unable to determine source range for offset " + offset + " in " + method.name);
			}
		}
		
		/* Find start line */
		for (i = 0; i < lines.Length; ++i)
			if (lines [i].Offset == beginOffset) {
				startLine = lines [i].Row;
				break;
			}

		//	g_assert (i < num_line_numbers);

		/* Find end line */
		if (lines.Length == 1)
			endLine = lines [0].Row;
		else {
			for (i = 0; i < lines.Length; ++i)
				if (i == lines.Length - 1) {
					endLine = lines [i].Row;
					break;
				}
				else if (lines [i + 1].Offset > endOffset) {
					endLine = lines [i + 1].Row - 1;
					break;
				}
		}

		return true;
	}

	private ClassCoverageItem ProcessClass (Type t) {
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

			klass.filtered = IsFiltered ("[" + t.Assembly + "]" + className);
			classes [className] = klass;
		}

		return klass;
	}

	private void ProcessMethod (MethodBase monoMethod, MethodEntry entry, ClassCoverageItem klass, string methodName, string cov_info) {
		if (entry == null)
			// Compiler generated, abstract method etc.
			return;

		LineNumberEntry[] lines = entry.LineNumbers;

		if (lines.Length == 0)
			return;

		int start_line = lines [0].Row;
		int end_line = lines [lines.Length - 1].Row;

		MethodCoverageItem method 
			= new MethodCoverageItem (klass, methodName);

		method.startLine = start_line;
		method.endLine = end_line;
		method.filtered = IsFiltered ("[" + monoMethod.DeclaringType.Assembly + "]" + monoMethod.DeclaringType + "::" + monoMethod.Name);
		klass.methodsByMethod [monoMethod] = method;


		if (klass.sourceFile == null) {
			string sourceFile = entry.SourceFile.FileName;

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
