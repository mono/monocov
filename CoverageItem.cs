
using System;
using System.Collections;

namespace MonoCov {

public abstract class CoverageItem {

	public int hit;
	public int missed;
	public double coveragePercent;

	public bool filtered;

	public CoverageItem parent;

	public ArrayList children;

	public CoverageItem () {
		hit = 0;
		missed = 0;
		coveragePercent = 0.0;
	}

	public CoverageItem (CoverageItem parent) : this () {
		if (parent != null)
			parent.AddChildren (this);
	}

	public void AddChildren (CoverageItem item) {
		if (children == null)
			children = new ArrayList ();
		children.Add (item);
		item.parent = this;
	}

	public virtual bool IsLeaf {
		get {
			return false;
		}
	}

	public int ChildCount {
		get {
			if (children == null)
				return 0;
			else
				return children.Count;
		}
	}

	public void setCoverage (int hit, int missed) {
		this.hit = hit;
		this.missed = missed;
		if (hit + missed == 0)
			coveragePercent = 100.0;
		else
			coveragePercent = (double)hit / (hit + missed);
	}

	public void computeCoveragePercent () {
		if (hit + missed == 0)
			coveragePercent = 100.0;
		else
			coveragePercent = (double)hit / (hit + missed);
	}

	public void computeCoverage () {
		computeCoverage (false);
	}

	public void computeCoverage (bool recurse) {
		if (IsLeaf)
			return;

		hit = 0;
		missed = 0;

		if (children != null) {
			foreach (CoverageItem item in children) {
				if (!item.filtered) {
					if (recurse)
						item.computeCoverage (recurse);
					hit += item.hit;
					missed += item.missed;
				}
			}
		}

		computeCoveragePercent ();
	}

	public void recomputeCoverage () {
		computeCoverage ();

		if (parent != null)
			parent.recomputeCoverage ();
	}

	public void FilterItem (bool isFiltered) {
		if (filtered != isFiltered) {
			filtered = isFiltered;
			recomputeCoverage ();
		}
	}

	public override string ToString () {
		return "" + GetType () + "(hit=" + hit + ", missed=" + missed + ")";
	}
}
}
