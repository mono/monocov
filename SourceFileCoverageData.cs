

using System;
using System.Collections;

namespace MonoCov {

public class SourceFileCoverageData {

	/// the name of the source file
	public string sourceFile;

	/// contains line_number->count mappings
	/// count > 0    -> hit
	/// count == 0   -> missed
	/// count == -1  -> comment/declaration/whitespace/no info
	public int[] coverage;

	// The list of methods which are part of this source file
	public ArrayList methods;

	public SourceFileCoverageData (string sourceFile) {
		this.sourceFile = sourceFile;
	}

	public void AddMethod (MethodCoverageItem method) {
		if (methods == null)
			methods = new ArrayList ();
		methods.Add (method);
	}

	public int[] Coverage {
		get {
			if (coverage != null)
				return coverage;

			if (methods == null)
				return new int [0];

			int endLine = 0;
			foreach (MethodCoverageItem method in methods) {
				if (method.endLine > endLine)
					endLine = method.endLine;
			}
			coverage = new int [endLine + 1];
			for (int i = 0; i < coverage.Length; ++i)
				coverage [i] = -1;

			foreach (MethodCoverageItem method in methods) {
				if (method.lineCoverage != null) {
					for (int i = 0; i < method.lineCoverage.Length; ++i)
						coverage [method.startLine + i] = method.lineCoverage [i];
				}
			}
			return coverage;
		}
	}
}
}
