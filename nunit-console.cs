#region Copyright (c) 2002, James W. Newkirk, Michael C. Two, Alexei A. Vorontsov, Philip A. Craig
/************************************************************************************
'
' Copyright © 2002 James W. Newkirk, Michael C. Two, Alexei A. Vorontsov
' Copyright © 2000-2002 Philip A. Craig
'
' This software is provided 'as-is', without any express or implied warranty. In no 
' event will the authors be held liable for any damages arising from the use of this 
' software.
' 
' Permission is granted to anyone to use this software for any purpose, including 
' commercial applications, and to alter it and redistribute it freely, subject to the 
' following restrictions:
'
' 1. The origin of this software must not be misrepresented; you must not claim that 
' you wrote the original software. If you use this software in a product, an 
' acknowledgment (see the following) in the product documentation is required.
'
' Portions Copyright © 2002 James W. Newkirk, Michael C. Two, Alexei A. Vorontsov 
' or Copyright © 2000-2002 Philip A. Craig
'
' 2. Altered source versions must be plainly marked as such, and must not be 
' misrepresented as being the original software.
'
' 3. This notice may not be removed or altered from any source distribution.
'
'***********************************************************************************/
#endregion

using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;
using System.Resources;
using System.Text;
using System.Collections;
using NUnit.Core;
using Mono.GetOptions;

[assembly: AssemblyTitle("nunit-console")]
[assembly: AssemblyDescription("NUnit console test runner")]
[assembly: AssemblyCopyright("Copyright (C) 2002 James W. Newkirk, Michael C. Two, Alexei A. Vorontsov. \nCopyright (C) 2000-2002 Philip Craig.\nAll Rights Reserved.")]
[assembly: Mono.Author("The NUNIT Guys && Zoltan Varga")]
[assembly: Mono.UsageComplement("<assembly name> [<fixture name>]")]
[assembly: Mono.About("")]

public class ConsoleOptions : Options
{
	[Option(3, "exclude test fixtures whose name matches PARAM", 'x', "exclude")]
		public string[] excludeList;

	[Option(3, "only run test fixtures whose name matches PARAM", 'i', "include")]
		public string[] includeList;
	[Option("do not print summary", "no-summary")]
		public bool noSummary = false;

	[Option("print names of tests as they are executed", 'v', "verbose")]
		public bool verbose = false;

	public override WhatToDoNext DoAbout() {
		base.DoAbout ();
		return WhatToDoNext.AbandonProgram;
	}
}

	public class ConsoleUi
	{
		private NUnit.Core.TestDomain testDomain;
		private string outputFile;
		private XmlTextReader transformReader;
		private static ConsoleOptions options;

		public static int Main(string[] args)
		{
			options = new ConsoleOptions ();
			options.ProcessArgs (args);
			args = options.RemainingArguments;

			NUnit.Core.TestDomain domain = new NUnit.Core.TestDomain();

			if (args.Length < 1) {
				options.DoUsage ();
				return 1;
			}

			string assembly = args [0];
			Console.WriteLine ("ASSEMBLY: " + assembly);

			Test test;

			if (args.Length == 1)
				test = domain.LoadAssembly (assembly);
			else
				test = domain.LoadAssembly (assembly, args [1]);
			if (test == null) {
				Console.Error.WriteLine("\nERROR: Unable to load test suite from assembly {0}", assembly);
				return 1;
			}
				
			Directory.SetCurrentDirectory(new FileInfo(assembly).DirectoryName);
			string xmlResult = "result.xml";

			XmlTextReader reader = GetTransformReader();
			ConsoleUi consoleUi = new ConsoleUi(domain, xmlResult, reader);
			return consoleUi.Execute();
		}

		private static XmlTextReader GetTransformReader()
		{
			XmlTextReader reader = null;
			Assembly assembly = Assembly.GetAssembly(typeof(XmlResultVisitor));
			ResourceManager resourceManager = new ResourceManager("NUnit.Framework.Transform",assembly);
			string xmlData = (string)resourceManager.GetObject("Summary.xslt");

			return new XmlTextReader(new StringReader(xmlData));
		}

		private static bool DoesFileExist(string fileName)
		{
			FileInfo fileInfo = new FileInfo(fileName);
			return fileInfo.Exists;
		}

		private static void WriteHelp(TextWriter writer)
		{
			writer.WriteLine("\n\n         NUnit console options\n");
			writer.WriteLine("/assembly:<assembly name>                            Assembly to test");
			writer.WriteLine("/fixture:<class name> /assembly:<assembly name>      Fixture or Suite to run");
			writer.WriteLine("\n\n         XML formatting options");
			writer.WriteLine("/xml:<file>                 XML result file to generate");
			writer.WriteLine("/transform:<file>           XSL transform file");
		}

		public ConsoleUi(NUnit.Core.TestDomain testDomain, string xmlFile, XmlTextReader reader)
		{
			this.testDomain = testDomain;
			outputFile = xmlFile;
			transformReader = reader;
		}

		public int Execute()
		{
			EventCollector collector = new EventCollector();
			Console.WriteLine ();

			ConsoleWriter outStream = new ConsoleWriter(Console.Out);
			ConsoleWriter errorStream = new ConsoleWriter(Console.Error);

			if (!options.noSummary) {
				TestResult result = testDomain.Run(collector, outStream, errorStream);
				Console.WriteLine("\n");
				XmlResultVisitor resultVisitor = new XmlResultVisitor(outputFile, result);
				result.Accept(resultVisitor);
				resultVisitor.Write();
				CreateSummaryDocument();
				return result.IsFailure ? 1 : 0;
			}
			else {
				testDomain.Run (collector, outStream, errorStream);
				return 0;
			}
		}

		private void CreateSummaryDocument()
		{
			XPathDocument originalXPathDocument = new XPathDocument (new FileStream (outputFile, FileMode.Open));
			XslTransform summaryXslTransform = new XslTransform();
			summaryXslTransform.Load(transformReader);
			
			summaryXslTransform.Transform(originalXPathDocument,null,Console.Out);
		}

		private class EventCollector : LongLivingMarshalByRefObject, EventListener
		{
			private int suiteLevel;

			public void TestFinished(TestCaseResult testResult)
			{
				if(testResult.Executed)
				{
					if(testResult.IsFailure)
					{	
						Console.Write("F");
					}
				}
				else
					Console.Write("N");
			}

			public void TestStarted(TestCase testCase)
			{
				if (!testCase.Suite.ShouldRun)
					return;

				if (options.verbose)
					Console.WriteLine (testCase.Name);
				Console.Write (".");
			}

			/*
			public void SuiteStarted(TestSuite suite) {
				if (suiteLevel == 0) {
					suiteLevel = 1;
					return;
				}
					
				Console.WriteLine ();
				for (int i = 0; i < suiteLevel - 1; ++i)
					Console.Write ("  ");

				Console.Write (suite.Name + " ");
				suiteLevel ++;
			}

			public void SuiteFinished(TestSuiteResult result) {
				suiteLevel --;
				Console.WriteLine ();

				if (suiteLevel > 0) {
					if (((Test)(((TestSuite)(result.Test)).Tests [0])).IsSuite) {
						for (int i = 0; i < suiteLevel - 1; ++i)
							Console.Write ("  ");
						Console.Write (result.Name);
					}
				}
			}
			*/

			public void SuiteStarted(TestSuite suite) {
				// Filtering tests here is really slow...
				if ((options.includeList != null) && (options.includeList.Length != 0)) {
					bool match = false;
					if ((suite.Tests.Count > 0) && ((Test)suite.Tests [0]).IsSuite)
						match = true;

					foreach (string pattern in options.includeList)
						if (suite.Name.IndexOf (pattern) != -1)
							match = true;
					if (!match) {
						suite.ShouldRun = false;
						return;
					}
				}

				foreach (string pattern in options.excludeList) {
					if (suite.Name.IndexOf (pattern) != -1) {
						Console.WriteLine ("SKIPPED -> " + suite.Name);
						suite.ShouldRun = false;
						break;
					}
				}

				if ((suite.Tests.Count > 0) && ((Test)suite.Tests [0]).IsSuite)
					return;

				Console.Write (suite.Name + "  ");
			}

			public void SuiteFinished(TestSuiteResult result) {
				TestSuite suite = (TestSuite)result.Test;

				if (suite.ShouldRun && ! ((suite.Tests.Count > 0) && ((Test)(suite.Tests [0])).IsSuite))
					Console.WriteLine ();
			}
		}

}
