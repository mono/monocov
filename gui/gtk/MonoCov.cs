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

namespace MonoCov.Gui.Gtk {

public class MonoCov {

	private static string CAPTION = "MonoCov " + Assembly.GetExecutingAssembly ().GetName ().Version.ToString ();
	private FileSelection openDialog;
	private CoverageView coverageView;

	Glade.XML xml;

	[Glade.Widget] Window main;
	[Glade.Widget] ScrolledWindow scrolledwindow1;
	
	public static int GuiMain (String[] args)
	{
		Application.Init ();

		MonoCov main = new MonoCov ();

		if (args.Length > 0)
			main.OpenFile (args[0]);

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
}
}
