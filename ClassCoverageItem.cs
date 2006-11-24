
using System;
using System.Collections;
using Mono.Cecil;

namespace MonoCov {

public class ClassCoverageItem : CoverageItem {

	/// the namespace to which this class belongs
	public string name_space;

	/// the scoped name of this class
	public string name;

	public SourceFileCoverageData sourceFile;

	// The type object representing this class
#if USE_REFLECTION
	public Type type;
#else
	public TypeDefinition type;
#endif

	// Contains MethodBase -> MethodCoverageData mappings
	public Hashtable methodsByMethod;

	public ClassCoverageItem (CoverageItem parent) : base (parent) {
		methodsByMethod = new Hashtable ();
	}

	public ArrayList Methods {
		get {
			if (children == null)
				return new ArrayList (0);
			else
				return children;
		}
	}

	public string FullName {
		get {
			if ((name_space == "") || (name_space == "<GLOBAL>"))
				return name;
			else
				return name_space + "." + name;
		}
	}

	public string Namespace {
		get {
			if (name_space == "<GLOBAL>")
				return "";
			else
				return name_space;
		}
	}
}
}
