
using System;
using System.Collections;

namespace MonoCov {

public class NamespaceCoverageItem : CoverageItem {

	public string ns;

	public NamespaceCoverageItem (CoverageItem parent, string ns) : base (parent) {
		this.ns = ns;
	}

	public NamespaceCoverageItem ParentNamespace {
		get {
			return (NamespaceCoverageItem)parent;
		}
	}
}
}
