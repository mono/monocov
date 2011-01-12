
using System.IO;
using Mono.Options;

namespace MonoCov
{
	public class MonoCovOptions
	{
		public MonoCovOptions()
		{
			optionSet = new OptionSet ();
			optionSet.Add ("export-xml=", "Export coverage data as XML into specified directory", v => exportXmlDir = v);
			optionSet.Add ("export-html=", "Export coverage data as HTML into specified directory", v => exportHtmlDir = v);
			optionSet.Add ("stylesheet=", "Use the specified XSL stylesheet for XML->HTML conversion", v => exportHtmlDir = v);
			optionSet.Add ("minClassCoverage=", "If a code coverage of a class is less than specified, the application exits with return code 1.", v => float.TryParse(v, out minClassCoverage));
			optionSet.Add ("minMethodeCoverage=", "If a code coverage of a methode is less than specified, the application exits with return code 1.", v => float.TryParse(v, out minMethodeCoverage));
			optionSet.Add ("no-progress", "No progress messages during the export process", v => quiet = v != null);
			optionSet.Add ("h|help", "Show this message and exit", v => showHelp = v != null);			
		}
		private OptionSet optionSet;
		
		public string exportXmlDir;
		public string exportHtmlDir;
		public string styleSheet;
		public float minClassCoverage = -1f;
		public float minMethodeCoverage = -1f;
		public bool quiet = false;
		public bool showHelp = false;

		public void ProcessArgs (string[] args)
		{			
			remainingArguments = optionSet.Parse (args).ToArray ();
		}

		public string[] RemainingArguments {
			get { return remainingArguments; }
		}
		private string[] remainingArguments = new string[0];
		
		public void WriteHelp(TextWriter textWriter)
		{
			textWriter.WriteLine("Usage: monocov [OPTIONS]+ [<DATAFILE>]");
			textWriter.WriteLine();
			textWriter.WriteLine("Options:");
			
			optionSet.WriteOptionDescriptions(textWriter);
		}
	}
}

