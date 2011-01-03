//
// GTK# impressions:
//  - there are no convinience functions, like new FooWidget (parent)
//  - the generated sources are hard to read because the API functions are
//    mixed with internal functions, export directives etc.
//  - the app is slow to start up, slower than Qt#. Afterwards, it is faster.
//  - the open file dialog requires 6 lines in Gtk#, and 2 lines in Qt#
//  - no way to set a file name filter in FileSelection(Dialog)
//  - Why can't I inherit from Widget as in Qt ???
//

using GLib;
using Gtk;
using Glade;
using GtkSharp;
using System;
using System.Reflection;
using System.IO;
using System.Drawing;

namespace MonoCov.Gui.Gtk {

public class MonoCovGui {

	private static string CAPTION = "MonoCov " + Constants.Version;
	private CoverageView coverageView;

	Glade.XML xml;

	[Glade.Widget] Window main;
	[Glade.Widget] ScrolledWindow scrolledwindow1;
	[Glade.Widget] VPaned vpaned1;
	[Glade.Widget] Notebook notebook1;
	[Glade.Widget] ProgressBar progressbar1;
	
	public static int GuiMain (String[] args)
	{
		Application.Init ();

		MonoCovGui main = new MonoCovGui ();

		// allow the app to show up first
		GLib.Idle.Add (delegate (){
			if (args.Length > 0)
				main.OpenFile (args[0]);
			return false;
		});

		Application.Run ();

		return 0;
	}

	public MonoCovGui ()
	{
		xml = new Glade.XML (typeof (MonoCovGui).Assembly, "monocov.glade", null, null);
		xml.Autoconnect (this);

		main.Title = CAPTION;
		vpaned1.Position = (int)(vpaned1.Allocation.Height / 3);
	}

	public void OnQuit (object o, EventArgs args)
	{
		Application.Quit ();
	}

	public void OnQuit (object o, DeleteEventArgs args)
	{
		Application.Quit ();
	}

	public void OnAbout (object o, EventArgs args)
	{
		AboutDialog dialog = new AboutDialog ();

		dialog.ProgramName = CAPTION;

		dialog.Authors = new string[] {
			"Zoltan Varga (vargaz@gmail.com)"
		};

		dialog.Copyright = "Copyright © 2008 Novell, Inc. and Others";
		dialog.Comments = "A Coverage Analysis program for MONO.";

		dialog.Run ();
		dialog.Destroy ();
	}

	private void OpenFile (string fileName)
	{
		//		if (coverageView != null)
		//			coverageView.Close (true);

		if (coverageView != null) {
			scrolledwindow1.Remove (coverageView.Widget);
			coverageView.ShowSource -= OnShowSource;

			while (notebook1.NPages != 0)
				notebook1.RemovePage (0);
		}

		progressbar1.Show ();

		try {
			coverageView = new CoverageView (fileName, progressbar1);
			coverageView.ShowSource += OnShowSource;

			main.Title = (CAPTION + " - " + new FileInfo (fileName).Name);

			scrolledwindow1.Add (coverageView.Widget);

			main.ShowAll ();
			// allow some time for user feedback
			GLib.Timeout.Add (1000, delegate {
				progressbar1.Hide ();
				return false;
			});
		} catch (Exception e) {
			if (coverageView != null) {
				scrolledwindow1.Remove (coverageView.Widget);
				coverageView.ShowSource -= OnShowSource;
			}
				
			coverageView = null;
			progressbar1.Hide ();
			main.Title = CAPTION;
				
			MessageDialog messageDialog = new MessageDialog (main, DialogFlags.DestroyWithParent, MessageType.Error, ButtonsType.Close, e.Message);
			messageDialog.Title = "Error";
			messageDialog.Run ();
			messageDialog.Destroy ();
		}
	}

	private void OnShowSource (object sender, CoverageView.ShowSourceEventArgs e)
	{
		foreach (Widget widget in notebook1.Children) {
			SourceWindow notebookSourceWindow = widget as SourceWindow;
			if (notebookSourceWindow == null)
				continue;

			if (notebookSourceWindow.classItem.Model.sourceFile.sourceFile != e.methodItem.ParentClass.Model.sourceFile.sourceFile)
				continue;

			notebook1.CurrentPage = notebook1.PageNum (notebookSourceWindow);
			notebookSourceWindow.CenterOnMethod (e.methodItem);
			return;
		}

		SourceWindow sourceWindow = new SourceWindow (e.methodItem.ParentClass);
		sourceWindow.CenterOnMethod (e.methodItem);

		string sourceFile = e.methodItem.ParentClass.Model.sourceFile.sourceFile;
		sourceFile = Path.GetFileName (sourceFile);

		int index = notebook1.AppendPage (sourceWindow, new Label (sourceFile));
		notebook1.CurrentPage = index;
	}

	private void ExportAsXml (string destDir)
	{
		coverageView.ExportAsXml (destDir);
		Environment.Exit (1);
	}
	
	public void delete_cb (object o, DeleteEventArgs args)
	{
		SignalArgs sa = (SignalArgs) args;
		Application.Quit ();
		sa.RetVal = true;
	}

	public void OnOpen (object o, EventArgs args)
	{
		string fileName = null;
		FileChooserDialog dialog =
			new FileChooserDialog ("Choose a file",
			                       main,
			                       FileChooserAction.Open,
					       Stock.Cancel, ResponseType.Cancel,
					       Stock.Ok, ResponseType.Ok);

		FileFilter filter = new FileFilter ();
		filter.AddPattern ("*.cov");
		dialog.AddFilter (filter);

		if (dialog.Run () == (int) ResponseType.Ok) {
			fileName = dialog.Filename;
		}

		dialog.Destroy ();

		if (fileName != null) {
			OpenFile (fileName);
		}
	}

	public void OnCloseTab (object o, EventArgs args)
	{
		if (notebook1 == null || notebook1.NPages == 0)
			return;

		notebook1.RemovePage (notebook1.CurrentPage);
	}
}
}
