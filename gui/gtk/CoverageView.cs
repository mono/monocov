

using Gtk;
using GLib;
using Gdk;
using Gnome;
using Glade;
using GtkSharp;

using System;
using System.Collections;

namespace MonoCov.Gui.Gtk {

public class CoverageView {

	public class TreeItem {
		public string title;
		public TreeStore store;
		public TreeItem parent;
		public TreeIter iter;
		public CoverageItem model;

		public TreeItem (TreeStore store, TreeItem parent, CoverageItem model, string title) {
			this.store = store;
			this.parent = parent;
			this.model = model;

			if (parent == null)
				iter = store.AppendValues (title);
			else
				iter = store.AppendValues (parent.Iter, title);
			FillColumns ();
		}

		public TreeIter Iter {
			get {
				return iter;
			}

			set {
				this.iter = value;
			}
		}

		public void FillColumns () {
			double coverage;

			if (model.hit + model.missed == 0)
				coverage = 1.0;
			else
				coverage = (double)model.hit / (model.hit + model.missed);

			store.SetValue (iter, 1, "" + model.hit);
			store.SetValue (iter, 2, "" + model.missed);

			string coveragePercent 
				= String.Format ("{0:###0}", coverage * 100);

			store.SetValue (iter, 3, coveragePercent + "%");

			store.SetValue (iter, 4, this);
		}
	}

	public class MethodItem : TreeItem {
		public MethodItem (TreeStore store, TreeItem parent, CoverageItem model, string title)
			: base (store, parent, model, title)
		{
		}
	}

	public class ClassItem : TreeItem {
		public ClassItem (TreeStore store, TreeItem parent, CoverageItem model, string title)
			: base (store, parent, model, title)
		{
		}
	}

	public class NamespaceItem : TreeItem {
		public NamespaceItem (TreeStore store, TreeItem parent, CoverageItem model, string title)
			: base (store, parent, model, title)
		{
		}
	}
	
	public static string[] DEFAULT_FILTERS = {
		"-PrivateImplementationDetails"
	};

	TreeView tree;
	Hashtable namespaces;
	Hashtable classes;
	CoverageModel model;
	
	public CoverageView (string fileName)
	{
		TreeStore store = new TreeStore (typeof (string), typeof (string), typeof (string), typeof (string), typeof (object));
		tree = new TreeView (store);

		CellRendererText renderer = new CellRendererText ();
		// LAME: Why is this property a float instead of a double ?
		renderer.Xalign = 0.5f;

		tree.AppendColumn ("Classes", new CellRendererText (), "text", 0);
		tree.AppendColumn ("Lines Hit", renderer, "text", 1);
		tree.AppendColumn ("Lines Missed", renderer, "text", 2);
		tree.AppendColumn ("Coverage", renderer, "text", 3);

		tree.GetColumn (0).Resizable = true;
		tree.GetColumn (1).Alignment = 0.5f;
		tree.GetColumn (1).Resizable = true;
		tree.GetColumn (2).Alignment = 0.5f;
		tree.GetColumn (2).Resizable = true;
		tree.GetColumn (3).Alignment = 0.5f;
		tree.GetColumn (3).Resizable = true;

		tree.HeadersVisible = true;

		model = new CoverageModel ();
		foreach (string filter in DEFAULT_FILTERS) {
			model.AddFilter (filter);
		}
		model.ReadFromFile (fileName);

		TreeItem root = new TreeItem (store, null, model, "PROJECT");

		Hashtable classes2 = model.Classes;

		namespaces = new Hashtable ();
		foreach (string name in classes2.Keys) {
			ClassCoverageItem klass = (ClassCoverageItem)classes2 [name];

			if (klass.filtered)
				continue;

			string namespace2 = klass.name_space;
			TreeItem nsItem = (TreeItem)namespaces [namespace2];
			if (nsItem == null) {
				nsItem = new TreeItem (store, root, (CoverageItem)model.Namespaces [namespace2], namespace2);
				//				nsItem.SetPixmap (0, namespaceOpenPixmap);
				namespaces [namespace2] = nsItem;
			}

			if (nsItem.model.filtered)
				continue;

			TreeItem classItem = new ClassItem (store, nsItem, klass, klass.name);
			//			classItem.SetPixmap (0, classPixmap);

			// We should create the method nodes only when the class item
			// is opened
			
			foreach (MethodCoverageItem method in klass.Methods) {
				if (method.filtered)
					continue;

				string title = method.Name;
				if (title.Length > 64)
					title = title.Substring (0, 63) + "...)";

				new MethodItem (store, classItem, method, title);
			}
		}

		tree.ExpandRow (store.GetPath (root.Iter), false);

		foreach (string ns in namespaces.Keys)
			tree.ExpandRow (store.GetPath (((TreeItem)namespaces [ns]).Iter), false);

		tree.ButtonPressEvent += new ButtonPressEventHandler (OnButtonPress);
		tree.Selection.Mode = SelectionMode.Single;

		// LAME: Why doesn't widgets visible by default ???
		tree.Show ();
	}

	void OnButtonPress (object o, ButtonPressEventArgs args)
	{
		if (args.Event.type == Gdk.EventType.TwoButtonPress) 
			OnDoubleClick ();
	}

	void OnDoubleClick ()
	{
		TreeModel model;
		TreeIter iter = new TreeIter ();
		
		if (!tree.Selection.GetSelected (out model, ref iter))
			return;

		GLib.Value value;
		model.GetValue (iter, 4, out value);
		object a = value.Val;

		Console.WriteLine ("Type is: " + a.GetType ().ToString ());
		if (tree.RowExpand (model.GetPath (iter))) {
			// LAME: This seems to collapse the entire tree...
			tree.CollapseRow (model.GetPath (iter));
		} else {
			tree.ExpandRow (model.GetPath (iter), false);
		}
	}
	
	/*
	public void SlotDoubleClick (QListViewItem item) {
		if (item is MethodItem) {
			MethodItem method = (MethodItem)item;

			ClassItem parent = method.parent;

			if (parent.sourceBrowser != null) {
				parent.sourceBrowser.ShowNormal ();
				parent.sourceBrowser.Raise ();
			}
			else {
				parent.sourceBrowser = new SourceWindow (this, parent);
				parent.sourceBrowser.Show ();
			}

			parent.sourceBrowser.CenterOnMethod (method);
		}
	}
	*/

	public Widget Widget {
		get {
			return tree;
		}
	}

	void FillCoverageColumns (TreeItem item, int hit, int missed) {
		double coverage;

		if (hit + missed == 0)
			coverage = 100.0;
		else
			coverage = (double)hit / (hit + missed);

		item.store.SetValue (item.iter, 1, "" + hit);
		item.store.SetValue (item.iter, 2, "" + missed);

		string coveragePercent 
			= String.Format ("{0:##0}%", coverage * 100);

		item.store.SetValue (item.iter, 3, coveragePercent);
	}

	public void ExportAsXml (string destDir)
	{
		XmlExporter exporter = new XmlExporter ();
		exporter.DestinationDir = destDir;
		exporter.Export (model);
		Environment.Exit (1);
	}

}
}
