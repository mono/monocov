using System;
using System.Collections;
using System.Xml;
using System.IO;
using System.Reflection;
using Mono.CompilerServices.SymbolWriter;

public class SymbolDumper
{

	public static void Main (String[] args)
	{
		if (args.Length != 2) {
			Console.WriteLine ("USAGE: symbols <ASSEMBLY> <METHOD NAME PATTERN>");
			Environment.Exit (1);
		}

		string assemblyName = args [0];
		string methodNamePattern = args [1];

		Assembly assembly = Assembly.LoadFrom (assemblyName);

		Console.WriteLine ("Reading symbols for " + assembly + " ...");
		MonoSymbolFile symbolFile = MonoSymbolFile.ReadSymbolFile (assembly);

		if (symbolFile == null)
			Console.WriteLine ("WARNING: No symbols found for " + assembly);
		else
			Console.WriteLine ("Loaded symbol info for " + symbolFile.SourceCount + " source files and " + symbolFile.MethodCount + " methods.");

		Module[] modules = assembly.GetModules();

		if (modules.Length > 1)
			Console.WriteLine("WARNING: Assembly had more than one module. Using the first.");

		Module module = modules[0];

		foreach (MethodEntry entry in symbolFile.Methods) {
		
			MethodBase methodBase = module.ResolveMethod(entry.Token);
				
			if (methodBase.Name.IndexOf (methodNamePattern) != -1) {
				Console.WriteLine (methodBase.DeclaringType.FullName + ":" + methodBase.Name + " " + entry);

				foreach (LineNumberEntry line in entry.LineNumbers)
					Console.WriteLine ("\t" + line);
			}					
		}
	}
}
