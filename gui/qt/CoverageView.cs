
using Qt;
using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace MonoCov.Gui.Qt {

public class TreeItem : QListViewItem {
	public CoverageItem model;

	public TreeItem (QListViewItem parent, String name, CoverageItem model) :
		base (parent, name) {
		this.model = model;

		FillColumns ();
	}

	public TreeItem (QListView parent, String name, CoverageItem model) :
		base (parent, name) {
		this.model = model;

		FillColumns ();
	}

	public void FillColumns () {
		double coverage;

		if (model.hit + model.missed == 0)
			coverage = 1.0;
		else
			coverage = (double)model.hit / (model.hit + model.missed);

		SetText (1, "" + model.hit);
		SetText (2, "" + model.missed);

		string coveragePercent 
			= String.Format ("{0:###0}", coverage * 100);

		SetText (3, coveragePercent + "%");
	}
}

public class NamespaceItem : TreeItem {
	public NamespaceItem (QListViewItem parent, String name, CoverageItem model) :
		base (parent, name, model) {
	}
}

public class ClassItem : TreeItem {
	public ClassItem (QListViewItem parent, String name, CoverageItem model) :
		base (parent, name, model) {
	}

	public ClassCoverageItem Model {
		get {
			return (ClassCoverageItem)model;
		}
	}
}

public class MethodItem : TreeItem {
	public MethodItem (QListViewItem parent, String name, CoverageItem model) :
		base (parent, name, model) {
	}

	public MethodCoverageItem Model {
		get {
			return (MethodCoverageItem)model;
		}
	}
}

public class CoverageView : QListView {

	public static string[] DEFAULT_FILTERS = {
		"-PrivateImplementationDetails"
	};

	// pixmaps stolen from some Qt example program...
	static string[] namespace_open_xpm = new string[] {
    "16 16 11 1",
    "# c #000000",
    "g c #c0c0c0",
    "e c #303030",
    "a c #ffa858",
    "b c #808080",
    "d c #a0a0a4",
    "f c #585858",
    "c c #ffdca8",
    "h c #dcdcdc",
    "i c #ffffff",
    ". c None",
    "....###.........",
    "....#ab##.......",
    "....#acab####...",
    "###.#acccccca#..",
    "#ddefaaaccccca#.",
    "#bdddbaaaacccab#",
    ".eddddbbaaaacab#",
    ".#bddggdbbaaaab#",
    "..edgdggggbbaab#",
    "..#bgggghghdaab#",
    "...ebhggghicfab#",
    "....#edhhiiidab#",
    "......#egiiicfb#",
    "........#egiibb#",
    "..........#egib#",
    "............#ee#"};

	static string[] namespace_closed_xpm = new string[] {
    "16 16 9 1",
    "g c #808080",
    "b c #c0c000",
    "e c #c0c0c0",
    "# c #000000",
    "c c #ffff00",
    ". c None",
    "a c #585858",
    "f c #a0a0a4",
    "d c #ffffff",
    "..###...........",
    ".#abc##.........",
    ".#daabc#####....",
    ".#ddeaabbccc#...",
    ".#dedeeabbbba...",
    ".#edeeeeaaaab#..",
    ".#deeeeeeefe#ba.",
    ".#eeeeeeefef#ba.",
    ".#eeeeeefeff#ba.",
    ".#eeeeefefff#ba.",
    ".##geefeffff#ba.",
    "...##gefffff#ba.",
    ".....##fffff#ba.",
    ".......##fff#b##",
    ".........##f#b##",
    "...........####."};

	static string[] class_xpm = new string[] {
    "16 16 7 1",
    "# c #000000",
    "b c #ffffff",
    "e c #000000",
    "d c #404000",
    "c c #c0c000",
    "a c #ffffc0",
    ". c None",
    "................",
    ".........#......",
    "......#.#a##....",
    ".....#b#bbba##..",
    "....#b#bbbabbb#.",
    "...#b#bba##bb#..",
    "..#b#abb#bb##...",
    ".#a#aab#bbbab##.",
    "#a#aaa#bcbbbbbb#",
    "#ccdc#bcbbcbbb#.",
    ".##c#bcbbcabb#..",
    "...#acbacbbbe...",
    "..#aaaacaba#....",
    "...##aaaaa#.....",
    ".....##aa#......",
    ".......##......."};

	private QListView table;
	private Hashtable namespaces;
	private Hashtable sourceViews;
	private CoverageModel model;

	public CoverageView (QWidget parent, String fileName) : base (parent) {
		SetRootIsDecorated (true);
		AddColumn ("Classes");
		AddColumn ("Lines Hit");
		AddColumn ("Lines Missed");
		AddColumn ("Coverage");

		sourceViews = new Hashtable ();

		// TODO: Why the cast ?
		SetColumnAlignment (1, (int)Qt.AlignmentFlags.AlignCenter);
		SetColumnAlignment (2, (int)Qt.AlignmentFlags.AlignCenter);
		SetColumnAlignment (3, (int)Qt.AlignmentFlags.AlignCenter);

		QObject.Connect (this, SIGNAL ("doubleClicked(QListViewItem)"),
						 this, SLOT ("OnDoubleClick(QListViewItem)"));

		QObject.Connect (this, SIGNAL ("expanded(QListViewItem)"),
						 this, SLOT ("OnExpanded(QListViewItem)"));

		// TODO: This is not supported by current Qt#
		try {
			QObject.Connect (this, SIGNAL ("contextMenuRequested(QListViewItem,QPoint,int)"),
							 this, SLOT ("OnContextMenu(QListViewItem, QPoint, Int32)"));
		}
		catch (Exception) {
		}

		QPixmap namespaceOpenPixmap = new QPixmap (namespace_open_xpm);
		QPixmap namespaceClosedPixmap = new QPixmap (namespace_closed_xpm);
		QPixmap classPixmap = new QPixmap (class_xpm);

		model = new CoverageModel ();
		foreach (string filter in DEFAULT_FILTERS) {
			model.AddFilter (filter);
		}
		model.ReadFromFile (fileName);

		QListViewItem rootItem = new TreeItem (this, "PROJECT", model);
		rootItem.SetOpen (true);

		Hashtable classes2 = model.Classes;

		namespaces = new Hashtable ();

		foreach (string name in classes2.Keys) {
			ClassCoverageItem klass = (ClassCoverageItem)classes2 [name];

			if (klass.filtered)
				continue;

			string namespace2 = klass.name_space;
			NamespaceItem nsItem = (NamespaceItem)namespaces [namespace2];
			if (nsItem == null) {
				// Create namespaces
				String nsPrefix = "";
				QListViewItem parentItem = rootItem;
				foreach (String nsPart in namespace2.Split ('.')) {
					if (nsPrefix == "")
						nsPrefix = nsPart;
					else
						nsPrefix = nsPrefix + "." + nsPart;

					NamespaceCoverageItem nsModel = (NamespaceCoverageItem)model.Namespaces [nsPrefix];
					if (nsModel.filtered)
						break;

					nsItem = (NamespaceItem)namespaces [nsPrefix];
					if (nsItem == null) {
						nsItem = new NamespaceItem (parentItem, nsPrefix,
													nsModel);
						nsItem.SetOpen (true);
						nsItem.SetPixmap (0, namespaceOpenPixmap);
						namespaces [nsPrefix] = nsItem;
					}
					parentItem = nsItem;
				}
			}

			if (nsItem != null) {
				ClassItem classItem = new ClassItem (nsItem, klass.name, klass);
				classItem.SetPixmap (0, classPixmap);
				if (klass.ChildCount > 0)
					classItem.SetExpandable (true);
			}
		}
	}

	private SourceWindow ShowSourceFor (ClassItem item) {
		SourceWindow sourceView = (SourceWindow)sourceViews [item.Model.sourceFile];
			if (sourceView != null) {
				sourceView.ShowNormal ();
				sourceView.Raise ();
			}
			else {
				sourceView = new SourceWindow (this, item);
				sourceViews [item.Model.sourceFile] = sourceView;
				sourceView.Show ();
			}
			return sourceView;
	}

	public void exportAsXml (string destDir) {
		XmlExporter exporter = new XmlExporter ();
		exporter.DestinationDir = destDir;
		exporter.Export (model);
		Environment.Exit (1);
	}

	private void OnDoubleClick (QListViewItem item) {
		if (item is MethodItem) {
			MethodItem method = (MethodItem)item;

			SourceWindow sourceView = ShowSourceFor ((ClassItem)method.Parent ());

			sourceView.CenterOnMethod (method);
		}
	}

	private void OnViewSource () {
		ShowSourceFor ((ClassItem)popupMenuItem);
	}

    private void OnExpanded (QListViewItem item) {
		if (item is ClassItem) {
			// Create children on-demand
			ClassItem classItem = (ClassItem)item;
			ClassCoverageItem klass = classItem.Model;

			if (item.ChildCount () == 0) {
				foreach (MethodCoverageItem method in klass.Methods) {
					string title = method.name;
					if (title.Length > 64)
						title = title.Substring (0, 63) + "...)";

					MethodItem methodItem = new MethodItem (classItem, title, method);
				}
			}
		}
	}

	private QListViewItem popupMenuItem;

	private void OnContextMenu (QListViewItem item, QPoint point, int col) {
		popupMenuItem = item;

		if (item is ClassItem) {
			ClassItem klass = (ClassItem)item;

			QPopupMenu menu = new QPopupMenu (this);

			menu.InsertItem ("Exclude from coverage", this, SLOT ("OnExclude()"));

			if (klass.Model.sourceFile != null)
				menu.InsertItem ("View Source", this, SLOT ("OnViewSource()"));

			menu.Exec (point);
		}
		else if (item is NamespaceItem) {
			QPopupMenu menu = new QPopupMenu (this);

			menu.InsertItem ("Exclude from coverage", this, SLOT ("OnExclude()"));
			menu.Exec (point);
		}
	}

	private void OnExclude () {
		TreeItem item = (TreeItem)popupMenuItem;
		if (item != null) {
			item.model.FilterItem (true);

			QListViewItem parent = item.Parent ();
			parent.TakeItem (item);
			while (parent is TreeItem) {
				((TreeItem)parent).FillColumns ();
				parent = parent.Parent ();
			}
		}
	}
}
}
