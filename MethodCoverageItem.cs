
using System;
using System.Collections;
using Mono.CompilerServices.SymbolWriter;

namespace MonoCov {

public class MethodCoverageItem : CoverageItem {

	public string name;
	public int startLine;
	public int endLine;

	public int[] lineCoverage;

	public MethodCoverageItem (ClassCoverageItem parent, String name) : base (parent) {
		this.name = name;
	}

	public ClassCoverageItem Class {
		get {
			return (ClassCoverageItem)parent;
		}
	}

	public override bool IsLeaf {
		get {
			return true;
		}
	}

	public string Name {
		get {
			return name;
		}
		set {
			name = value;
		}
	}
}
}
