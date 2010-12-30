//
// TODO:
// - the line number information generated for some methods is funky, like
//   System.Text.Encoding::get_ASCII
// - the line number information for System.CurrentTimeZone:GetDaylightChanges
//   contains a 0->196 mapping !!!
// - the line number information for life.cs does not contain anything for line
//   38.
// - when a method signature consists of multiple lines, the begin_line for
//   the methods seems to be the last line of the signature, instead of the 
//   first.
// - the line number information does not contain columns, making it
//   impossible to determine coverage for code like this:
//     if (something) { something }
// - why doesn't some line numbers do not appear in stack traces ????

using System;
using System.Reflection;
using System.IO;
using System.Text;
#if GUI_qt
using MonoCov.Gui.Qt;
#else
using MonoCov.Gui.Gtk;
#endif

[assembly: AssemblyTitle("monocov")]
[assembly: AssemblyDescription("A Coverage Analysis program for .NET")]
[assembly: AssemblyCopyright("Copyright (C) 2003 Zoltan Varga")]
[assembly: AssemblyVersion(Constants.Version)]

namespace MonoCov {

//
// This class should be named MonoCov, but the GUI class is already
// named that way, and it is hard to rename things in CVS...
//

public class MonoCovMain {

	public static int Main (string[] args) {
		MonoCovOptions options = new MonoCovOptions ();
		options.ProcessArgs (args);
		args = options.RemainingArguments;

		if (options.showHelp) {
			options.WriteHelp(Console.Out);
			return 0;
		}

		if (options.exportXmlDir != null)
			return handleExportXml (options, args);

		if (options.exportHtmlDir != null)
			return handleExportHtml (options, args);

		#if GUI_qt
		return MonoCov.Gui.Qt.MonoCov.GuiMain (args);
		#else
		return MonoCov.Gui.Gtk.MonoCovGui.GuiMain (args);
		#endif
	}

	private static void progressListener (object sender, XmlExporter.ProgressEventArgs e) {
		Console.Write ("\rExporting Data: " + (e.pos * 100 / e.itemCount) + "%");
	}

	private static int handleExportXml (MonoCovOptions opts, string[] args) {
		if (args.Length == 0) {
			Console.WriteLine ("Error: Datafile name is required when using --export-xml");
			return 1;
		}
		
		if (!File.Exists(args [0]))
		{
			Console.WriteLine(string.Format("Error: Datafile '{0}' not found.", args [0]));
			return 1;
		}
			
		if (!Directory.Exists (opts.exportXmlDir)) {
			try {
				Directory.CreateDirectory (opts.exportXmlDir);
			}
			catch (Exception ex) {
				Console.WriteLine ("Error: Destination directory '" + opts.exportXmlDir + "' does not exist and could not be created: " + ex);
				return 1;
			}
		}
			
		try {
			CoverageModel model = new CoverageModel ();
			model.ReadFromFile (args [0]);
			XmlExporter exporter = new XmlExporter ();
			exporter.DestinationDir = opts.exportXmlDir;
			exporter.StyleSheet = opts.styleSheet;
			if (!opts.quiet)
				exporter.Progress += new XmlExporter.ProgressEventHandler (progressListener);
			exporter.Export (model);
		}
		catch (Exception e) {
			Console.WriteLine("Error: "+e.Message);
			return 1;
		}
			
		if (!opts.quiet) {
			Console.WriteLine ();
			Console.WriteLine ("Done.");
		}
		return 0;
	}

	private static void htmlProgressListener (object sender, HtmlExporter.ProgressEventArgs e) {
		Console.Write ("\rExporting Data: " + (e.pos * 100 / e.itemCount) + "%");
	}

	private static int handleExportHtml (MonoCovOptions opts, string[] args) {
		if (args.Length == 0) {
			Console.WriteLine ("Error: Datafile name is required when using --export-html");
			return 1;
		}
		
		if (!File.Exists(args [0])) {
			Console.WriteLine(string.Format("Error: Datafile '{0}' not found.", args [0]));
			return 1;
		}
			
		if (!Directory.Exists (opts.exportHtmlDir)) {
			try {
				Directory.CreateDirectory (opts.exportHtmlDir);
			}
			catch (Exception ex) {
				Console.WriteLine ("Error: Destination directory '" + opts.exportHtmlDir + "' does not exist and could not be created: " + ex);
				return 1;
			}
		}
		
		try {
			CoverageModel model = new CoverageModel ();
			model.ReadFromFile (args [0]);
			HtmlExporter exporter = new HtmlExporter ();
			exporter.DestinationDir = opts.exportHtmlDir;
			exporter.StyleSheet = opts.styleSheet;
			if (!opts.quiet)
				exporter.Progress += new HtmlExporter.ProgressEventHandler (htmlProgressListener);
			exporter.Export (model);
		}
		catch (Exception e) {
			Console.WriteLine("Error: "+e.Message);
			return 1;
		}
				
		if (!opts.quiet) {
			Console.WriteLine ();
			Console.WriteLine ("Done.");
		}
		return 0;
	}
}
}
