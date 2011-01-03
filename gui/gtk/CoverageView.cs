//
// Missing:
//    DoubleClick action:
//       DoubleClick onmethod, call ShowSrouceFor (method.Parent)
//    Context menu.
//    Images for namespaces open/close and class.
//

using Gtk;
using GLib;
using Gdk;
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
		public ClassItem ParentClass;
		
		public MethodItem (TreeStore store, TreeItem parent, ClassItem parent_class, CoverageItem model, string title)
			: base (store, parent, model, title)
		{
			ParentClass = parent_class;
		}
		
		public MethodCoverageItem Model {
			get {
				return (MethodCoverageItem)model;
			}
		}
	}

	public class ClassItem : TreeItem {
		public ClassItem (TreeStore store, TreeItem parent, CoverageItem model, string title)
			: base (store, parent, model, title)
		{
		}

		public ClassCoverageItem Model {
			get {
				return (ClassCoverageItem) model;
			}
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
	TreeStore store;
	Hashtable namespaces;
	CoverageModel model;
	ProgressBar status;
	
	public CoverageView (string fileName, ProgressBar status)
	{
		store = new TreeStore (typeof (string), typeof (string), typeof (string), typeof (string), typeof (object));
		tree = new TreeView (store);

		CellRendererText renderer = new CellRendererText ();
		CellRendererText coverageRenderer = new CellRendererText ();
		// LAME: Why is this property a float instead of a double ?
		renderer.Xalign = 0.5f;

		tree.AppendColumn ("Classes", new CellRendererText (), "text", 0);
		tree.AppendColumn ("Lines Hit", renderer, "text", 1);
		tree.AppendColumn ("Lines Missed", renderer, "text", 2);
		tree.AppendColumn ("Coverage", coverageRenderer, "text", 3);

		tree.GetColumn (0).Resizable = true;
		tree.GetColumn (1).Alignment = 0.0f;
		tree.GetColumn (1).Resizable = true;
		tree.GetColumn (2).Alignment = 0.0f;
		tree.GetColumn (2).Resizable = true;
		tree.GetColumn (3).Alignment = 0.0f;
		tree.GetColumn (3).Resizable = true;
		tree.GetColumn (3).SetCellDataFunc (coverageRenderer, new TreeCellDataFunc (RenderCoverage));

		tree.HeadersVisible = true;

		model = new CoverageModel ();
		foreach (string filter in DEFAULT_FILTERS) {
			model.AddFilter (filter);
		}
		this.status = status;
		model.Progress += Progress;
		model.ReadFromFile (fileName);

		TreeItem root = new TreeItem (store, null, model, "PROJECT");

		Hashtable classes2 = model.Classes;

		namespaces = new Hashtable ();
		string[] sorted_names = new string [classes2.Count];
		classes2.Keys.CopyTo (sorted_names, 0);
		Array.Sort (sorted_names);
		Progress ("Building tree", 0.95);
		foreach (string name in sorted_names) {
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

			ClassItem classItem = new ClassItem (store, nsItem, klass, klass.name);

			if (klass.ChildCount != 0) {
				TreeIter treeIter = store.AppendNode (classItem.iter);
				store.SetValues (treeIter, "<loading>");
			}
		}

		tree.ExpandRow (store.GetPath (root.Iter), false);

		// it becomes very hard to navigate if everything is expanded
		//foreach (string ns in namespaces.Keys)
		//	tree.ExpandRow (store.GetPath (((TreeItem)namespaces [ns]).Iter), false);

		tree.RowExpanded += new RowExpandedHandler (OnRowExpanded);
		tree.RowCollapsed += new RowCollapsedHandler (OnRowCollapsed);
		tree.ButtonPressEvent += new ButtonPressEventHandler (OnButtonPress);
		tree.Selection.Mode = SelectionMode.Single;

		Progress ("Done", 1.0);
		// LAME: Why doesn't widgets visible by default ???
		tree.Show ();
	}

	void Progress (string part, double percent) {
		status.Fraction = percent;
		status.Text = part;
		while (Application.EventsPending ())
			Application.RunIteration ();
	}

	[GLib.ConnectBefore]
	void OnButtonPress (object o, ButtonPressEventArgs args)
	{
		if (args.Event.Type == Gdk.EventType.TwoButtonPress) 
			OnDoubleClick ();
	}

	public class ShowSourceEventArgs : EventArgs
	{
		public MethodItem methodItem;
	}

	public delegate void ShowSourceEventHandler (object sender, ShowSourceEventArgs e);

	public event ShowSourceEventHandler ShowSource;

	protected virtual void OnShowSource (ShowSourceEventArgs e)
	{
		if (ShowSource != null)
			ShowSource (this, e);
	}

	protected virtual void OnShowSource (MethodItem methodItem)
	{
		ShowSourceEventArgs e = new ShowSourceEventArgs ();
		e.methodItem = methodItem;

		OnShowSource (e);
	}

	private void RenderCoverage (TreeViewColumn column, CellRenderer cell, TreeModel model, TreeIter iter)
	{
		string text = (string)model.GetValue (iter, 3);
		int coverage = Int32.Parse(text.Substring(0, text.Length - 1));
		
		if(coverage > 99)
			(cell as CellRendererText).Foreground = "darkgreen";
		else if(coverage > 74)
			(cell as CellRendererText).Foreground = "gold";
		else if(coverage > 49)
			(cell as CellRendererText).Foreground = "OrangeRed";
		else
			(cell as CellRendererText).Foreground = "FireBrick";
	}

	void OnDeleteEvent (object sender, DeleteEventArgs e)
	{
	}
	
	void OnDoubleClick ()
	{
		TreeModel model;
		TreeIter iter;
		
		if (!tree.Selection.GetSelected (out model, out iter))
			return;

		GLib.Value value = new GLib.Value ();
		model.GetValue (iter, 4, ref value);
		object item = value.Val;

		if (item is MethodItem){
			OnShowSource (item as MethodItem);
		} else {
			if (tree.ExpandRow (model.GetPath (iter), true)) {
				// LAME: This seems to collapse the entire tree...
				tree.CollapseRow (model.GetPath (iter));
			} else {
				tree.ExpandRow (model.GetPath (iter), false);
			}
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

	private void OnRowExpanded (object sender, RowExpandedArgs args)
	{
		ClassItem classItem = store.GetValue (args.Iter, 4) as ClassItem;
		if (classItem == null)
			return;

		foreach (MethodCoverageItem method in classItem.Model.Methods) {
			if (method.filtered)
				continue;

			string title = method.Name;
			if (title.Length > 64)
				title = title.Substring (0, 63) + "...)";

			new MethodItem (store, classItem, classItem, method, title);
		}

		TreeIter treeIter = classItem.iter;
		store.IterChildren (out treeIter, treeIter);

		while (store.IterIsValid (treeIter)) {
			if (store.GetValue (treeIter, 4) is MethodItem)
				store.IterNext (ref treeIter);
			else
				store.Remove (ref treeIter);
		}
	}

	private void OnRowCollapsed (object sender, RowCollapsedArgs args)
	{
		if (!(store.GetValue (args.Iter, 4) is ClassItem))
			return;

		TreeIter treeIter = store.InsertNode (args.Iter, 0);
		store.SetValues (treeIter, "<loading>");
		store.IterNext (ref treeIter);

		while (store.IterIsValid (treeIter))
			store.Remove (ref treeIter);
	}

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
	}

}
}
