
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

// QT# problems:
// - open a second .cov file + exit -> crash in qt_del_QListViewItem
// - open a second .cov file + click on class name -> strange things happen
// - unable to override QListViewItem::SetOpen() because it is not virtual
// - themes does not work (i.e. new QPlatinumStyle () crashes)
//

using Qt;
using System;
using System.Reflection;
using System.IO;
using System.Text;
using Mono.GetOptions;

[assembly: AssemblyTitle("monocov")]
[assembly: AssemblyDescription("A Coverage Analysis program for .NET")]
[assembly: AssemblyCopyright("Copyright (C) 2003 Zoltan Varga")]
[assembly: Mono.Author("Zoltan Varga (vargaz@freemail.hu)")]
[assembly: AssemblyVersion("0.1")]
[assembly: Mono.UsageComplement("[<datafile>]")]
[assembly: Mono.About("")]

namespace MonoCov.Gui.Qt {

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

	public override WhatToDoNext DoAbout() {
		base.DoAbout ();
		return WhatToDoNext.AbandonProgram;
	}
}

public class MonoCov : QMainWindow {

	private static string CAPTION = "MonoCov " + Assembly.GetExecutingAssembly ().GetName ().Version.ToString ();

	private CoverageView coverageView;

	public static int Main (String[] args) {
		MonoCovOptions options = new MonoCovOptions ();
		options.ProcessArgs (args);
		args = options.RemainingArguments;

		if (options.exportXmlDir != null)
			return handleExportXml (options, args);

		if (options.exportHtmlDir != null)
			return handleExportHtml (options, args);

		QApplication app = new QApplication (args);

		MonoCov main = new MonoCov ();
		app.SetMainWidget (main);

		if (args.Length > 0)
			main.openFile (args[0]);

		if (args.Length > 1)
			main.exportAsXml (args [1]);

		main.Show ();

		return app.Exec ();
	}

	public MonoCov () : base (null) {
		coverageView = null;

		SetCaption (CAPTION);

		QPopupMenu fileMenu = new QPopupMenu (this);
		fileMenu.InsertItem ("&Open", this, SLOT ("SlotOpen()"));
		fileMenu.InsertItem ("E&xit", qApp, SLOT ("quit()"));

		//QPopupMenu editMenu = new QPopupMenu (this);
		//editMenu.InsertItem ("&Filters", this, SLOT ("SlotFilters()"));

		QPopupMenu aboutMenu = new QPopupMenu (this);
		aboutMenu.InsertItem ("&About", this, SLOT ("SlotAbout()"));

		QMenuBar menu = new QMenuBar (this);
		menu.InsertItem ("&File", fileMenu);
		//menu.InsertItem ("&Edit", editMenu);
		menu.InsertItem ("&About", aboutMenu);
	}

	public void SlotOpen () {
		string fileName = 
			QFileDialog.GetOpenFileName ("", "Coverage Files (*.cov)", this,
										 "", "Choose a file", "Coverage Files (*.cov)");
		if (fileName != null)
			openFile (fileName);
	}

	private void openFile (string fileName) {
		if (coverageView != null)
			coverageView.Close (true);

		QApplication.SetOverrideCursor (new QCursor ((int)Qt.CursorShape.WaitCursor));
		coverageView = new CoverageView (this, fileName);
		QApplication.RestoreOverrideCursor ();

		// TODO: How to tell Qt to set a good size automatically ???
		coverageView.SetMinimumSize (coverageView.SizeHint ());
		this.SetCentralWidget (coverageView);

		SetCaption (CAPTION + " - " + new FileInfo (fileName).Name);

		coverageView.Show ();
	}

	private void exportAsXml (string destDir) {
		coverageView.exportAsXml (destDir);
	}

	public void SlotAbout () {
		QMessageBox.About (this, "About MonoCov",
						   "<p>A Coverage Analysis program for MONO." +
						   "<p>By Zoltan Varga (vargaz@freemail.hu)" +
						   "<p>Powered by" +
						   "<p>MONO (<a href>http://www.go-mono.com</a>)" + 
						   "<p>and Qt# (<a href>http://qtcsharp.sourceforge.net</a>)");
	}

	FilterDialog filterDialog;

	public void SlotFilters () {
		if (filterDialog == null) {
			QDialog realDialog = (QDialog)QWidgetFactory.Create ("filterdialog.ui").QtCast ();
			filterDialog = new FilterDialog (realDialog);
		}
		filterDialog.Show ();
	}

	private static void progressListener (object sender, XmlExporter.ProgressEventArgs e) {
		Console.Write ("\rExporting Data: " + (e.pos * 100 / e.itemCount) + "%");
	}

	private static int handleExportXml (MonoCovOptions opts, string[] args) {
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

	private static void htmlProgressListener (object sender, HtmlExporter.ProgressEventArgs e) {
		Console.Write ("\rExporting Data: " + (e.pos * 100 / e.itemCount) + "%");
	}

	private static int handleExportHtml (MonoCovOptions opts, string[] args) {
		if (args.Length == 0) {
			Console.WriteLine ("Error: Datafile name is required when using --export-html");
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
		
		CoverageModel model = new CoverageModel ();
		model.ReadFromFile (args [0]);
		HtmlExporter exporter = new HtmlExporter ();
		exporter.DestinationDir = opts.exportHtmlDir;
		exporter.StyleSheet = opts.styleSheet;
		if (!opts.quiet)
			exporter.Progress += new HtmlExporter.ProgressEventHandler (htmlProgressListener);
		exporter.Export (model);
		if (!opts.quiet) {
			Console.WriteLine ();
			Console.WriteLine ("Done.");
		}
		return 0;
	}
}
}
