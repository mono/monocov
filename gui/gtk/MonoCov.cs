
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
using System.IO;
using System.Drawing;

namespace MonoCov.Gui.Gtk {

public class MonoCov {

	private static string CAPTION = "MonoCov 0.1";

	private FileSelection openDialog;

	private CoverageView coverageView;

	Glade.XML xml;

	Window main;

	public MonoCov () {
		xml = new Glade.XML (typeof (MonoCov).Assembly, "monocov.glade",
							 null, null);

		main = (Window)xml ["main"];
		main.Title = CAPTION;

		xml.Autoconnect (this);
	}

	public static int Main (String[] args) {

		Application.Init ();

		MonoCov main = new MonoCov ();

		if (args.Length > 0)
			main.OpenFile (args[0]);
 
		Application.Run ();

		return 0;
	}

	public void OnQuit (object o, EventArgs args) {
		Application.Quit ();
	}

	public void OnAbout (object o, EventArgs args) {
		MessageDialog dialog = 
			new MessageDialog (main, DialogFlags.Modal, MessageType.Info,
							   ButtonsType.Ok,
							   "A Coverage Analysis program for MONO.\n" +
							   "By Zoltan Varga (vargaz@freemail.hu)\n" +
							   "Powered by\n" +
							   "MONO (http://www.go-mono.com)\n" + 
							   "and Gtk# (http://gtk-sharp.sourceforge.net)");
		dialog.Run ();
		dialog.Destroy ();
	}

	private void OpenFile (string fileName) {
		//		if (coverageView != null)
		//			coverageView.Close (true);

		coverageView = new CoverageView (fileName);

		// TODO: How to tell Qt to set a good size automatically ???
		//		coverageView.SetMinimumSize (coverageView.SizeHint ());
		//		this.SetCentralWidget (coverageView);

		main.Title = (CAPTION + " - " + new FileInfo (fileName).Name);

		//		Console.WriteLine ("A: " + (Container)xml ["bonobodock1"]);
		((Container)xml ["scrolledwindow1"]).Add (coverageView.Widget);

		main.ShowAll ();
	}

    public void delete_cb (object o, DeleteEventArgs args) {
		SignalArgs sa = (SignalArgs) args;
		Application.Quit ();
		sa.RetVal = true;
	}

	public void file_sel_delete_event (object o, DeleteEventArgs args) {
		if (openDialog != null)
			openDialog.Destroy ();
	}

	public void file_sel_ok_event (object o, EventArgs args) {
		string fileName = openDialog.Filename;
		openDialog.Destroy ();
		OpenFile (fileName);
	}

	public void file_sel_cancel_event (object o, EventArgs args) {
		openDialog.Destroy ();
		openDialog = null;
	}

	public void OnOpen (object o, EventArgs args) {
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
