
//
// TODO:
// - Why there is no implementation for Diagnostics.SymbolStore for mono???
// - the line number information generated for some methods is funky, like
//   System.Text.Encoding::get_ASCII
// - the line number information does not contain columns, making it
//   impossible to determine coverage for code like this:
//     if (something) { something }
// - mono does not respect precision specifiers eg. 
//   String.Format ("{0:f2}", 3.141592377) returns 3.14159....
//

//
// GTK# impressions:
//  - there are no convinience functions, like new FooWidget (parent)
//  - the generated sources are hard to read because the API functions are
//    mixed with internal functions, export directives etc.
//  - the app is slow to start up, slower than Qt#. Afterwards, it is faster.
//  - the open file dialog requires 30 lines in Gtk#, and 2 lines in Qt#
//  - no way to set a file name filter in FileSelection(Dialog)
//  - Why can't I inherit from Widget as in Qt ???
//

using Gtk;
using Gnome;
using Glade;
using GtkSharp;
using System;
using System.Reflection;
using System.IO;
using System.Drawing;
using Mono.GetOptions;

[assembly: AssemblyTitle("monocov")]
[assembly: AssemblyDescription("A Coverage Analysis program for .NET")]
[assembly: AssemblyCopyright("Copyright (C) 2003 Zoltan Varga")]
[assembly: Mono.Author("Zoltan Varga (vargaz@freemail.hu)")]
[assembly: AssemblyVersion("0.1")]
[assembly: Mono.UsageComplement("[<datafile>]")]
[assembly: Mono.About("")]

namespace MonoCov.Gui.Gtk {

public class MonoCovOptions : Options
{
	[Option("Export coverage data as XML into directory PARAM", "export-xml")]
	public string exportXmlDir;

	[Option("Export coverage data as HTML into directory PARAM", "export-html")]
	public string exportHtmlDir;

	[Option("Use the XSL stylesheet PARAM for XML->HTML conversion", "stylesheet")]
	public string styleSheet;

	[Option("No progress messages during the export process", "no-progress")]
	public bool quiet = false;

	public override WhatToDoNext DoAbout ()
	{
		base.DoAbout ();
		return WhatToDoNext.AbandonProgram;
	}
}

public class MonoCov {

	private static string CAPTION = "MonoCov " + Assembly.GetExecutingAssembly ().GetName ().Version.ToString ();
	private FileSelection openDialog;
	private CoverageView coverageView;

	Glade.XML xml;

	[Glade.Widget] Window main;
	[Glade.Widget] ScrolledWindow scrolledwindow1;
	
	public static int Main (String[] args)
	{
		MonoCovOptions options = new MonoCovOptions ();
		options.ProcessArgs (args);
		args = options.RemainingArguments;

		if (options.exportXmlDir != null)
			return HandleExportXml (options, args);

		if (options.exportHtmlDir != null)
			return HandleExportHtml (options, args);
		
		Application.Init ();

		MonoCov main = new MonoCov ();

		if (args.Length > 0)
			main.OpenFile (args[0]);

		if (args.Length > 1)
			main.ExportAsXml (args [1]);

		Application.Run ();

		return 0;
	}

	public MonoCov ()
	{
		xml = new Glade.XML (typeof (MonoCov).Assembly, "monocov.glade", null, null);
		xml.Autoconnect (this);

		main.Title = CAPTION;
	}

	public void OnQuit (object o, EventArgs args)
	{
		Application.Quit ();
	}

	public void OnAbout (object o, EventArgs args)
	{
		MessageDialog dialog;

		dialog = new MessageDialog (main, DialogFlags.Modal, MessageType.Info,
					    ButtonsType.Ok,
					    "A Coverage Analysis program for MONO.\n" +
					    "By Zoltan Varga (vargaz@freemail.hu)\n" +
					    "Powered by\n" +
					    "MONO (http://www.go-mono.com)\n" + 
					    "and Gtk# (http://gtk-sharp.sourceforge.net)");
		dialog.Run ();
		dialog.Destroy ();
	}

	private void OpenFile (string fileName)
	{
		//		if (coverageView != null)
		//			coverageView.Close (true);

		coverageView = new CoverageView (fileName);

		main.Title = (CAPTION + " - " + new FileInfo (fileName).Name);

		scrolledwindow1.Add (coverageView.Widget);

		main.ShowAll ();
	}

	private void ExportAsXml (string destDir)
	{
		coverageView.ExportAsXml (destDir);
	}
	
	public void delete_cb (object o, DeleteEventArgs args)
	{
		SignalArgs sa = (SignalArgs) args;
		Application.Quit ();
		sa.RetVal = true;
	}

	public void file_sel_delete_event (object o, DeleteEventArgs args)
	{
		if (openDialog != null)
			openDialog.Destroy ();
	}

	public void file_sel_ok_event (object o, EventArgs args)
	{
		string fileName = openDialog.Filename;
		openDialog.Destroy ();
		OpenFile (fileName);
	}

	public void file_sel_cancel_event (object o, EventArgs args)
	{
		openDialog.Destroy ();
		openDialog = null;
	}

	public void OnOpen (object o, EventArgs args)
	{
		FileSelection dialog = 
			new FileSelection ("Choose a file");

		// TODO: How to set a filter ???

		dialog.HideFileopButtons ();

		dialog.Filename = "*.cs";

		dialog.DeleteEvent += new DeleteEventHandler (file_sel_delete_event);

		dialog.OkButton.Clicked +=new EventHandler (file_sel_ok_event);

		dialog.CancelButton.Clicked +=new EventHandler (file_sel_cancel_event);

		openDialog = dialog;

		dialog.Modal = true;
		dialog.ShowAll ();
	}

	static int HandleExportXml (MonoCovOptions opts, string[] args)
	{
		if (args.Length == 0) {
			Console.WriteLine ("Error: Datafile name is required when using --export-xml");
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
		
		CoverageModel model = new CoverageModel ();
		model.ReadFromFile (args [0]);
		XmlExporter exporter = new XmlExporter ();
		exporter.DestinationDir = opts.exportXmlDir;
		exporter.StyleSheet = opts.styleSheet;
		if (!opts.quiet)
			exporter.Progress += new XmlExporter.ProgressEventHandler (progressListener);
		exporter.Export (model);
		if (!opts.quiet) {
			Console.WriteLine ();
			Console.WriteLine ("Done.");
		}
		return 0;
	}

	static void progressListener (object sender, XmlExporter.ProgressEventArgs e)
	{
		Console.Write ("\rExporting Data: " + (e.pos * 100 / e.itemCount) + "%");
	}

	static int HandleExportHtml (MonoCovOptions opts, string[] args)
	{
		Console.WriteLine ("Not yet.");
		Console.WriteLine ("Use --export-xml and view the generated files with an XSL aware browser like mozilla.");
		return 1;
	}
}
}
