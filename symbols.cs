
using System;
using System.Collections;
using System.Xml;
using System.IO;
using System.Reflection;
using Mono.CSharp.Debugger;

public class SymbolDumper {

	public static void Main (String[] args) {
		if (args.Length != 2) {
			Console.WriteLine ("USAGE: symbols <ASSEMBLY> <NETHOD NAME PATTERN>");
			Environment.Exit (1);
		}
		string assemblyName = args [0];
		string methodNamePattern = args [1];

		Assembly assembly = Assembly.LoadFrom (args [0]);

		Console.WriteLine ("Reading symbols for " + assembly + " ...");
		MonoSymbolFile symbolFile = MonoSymbolFile.ReadSymbolFile (assembly);
		if (symbolFile == null)
			Console.WriteLine ("WARNING: No symbols found for " + assembly);
		else {
			Console.WriteLine ("Loaded symbol info for " + symbolFile.SourceCount + " source files and " + symbolFile.MethodCount + " methods.");
		}

		for (int i = 0; i < symbolFile.MethodCount; ++i) {
			MethodEntry entry = symbolFile.GetMethod (i + 1);

			LineNumberEntry[] lines = entry.LineNumbers;

			if (entry.FullName.IndexOf (methodNamePattern) != -1) {
				Console.WriteLine ("METHOD: " + entry.FullName + "[" + entry.Token + "]");

				foreach (LineNumberEntry line in lines)
					Console.WriteLine ("\t" + line);
			}
	}
}
}
