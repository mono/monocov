
using Qt;
using System;
using System.IO;
using System.Text;
using System.Collections;

namespace MonoCov.Gui.Qt {

public class FilterDialog {

	QDialog dialog;

	ArrayList filters = new ArrayList ();
	ArrayList enabled = new ArrayList ();

	public FilterDialog (QDialog dialog) {
		this.dialog = dialog;

		filters.Add ("NOT YET");
		enabled.Add (true);

		// TODO: Add indexer
		QListView list = (QListView)dialog.Child ("list").QtCast ();
		list.SetResizeMode (QListView.ResizeMode.LastColumn);

		for (int i = 0; i < filters.Count; ++i) {
			string pattern = (string)filters [i];
			QCheckListItem item = new QCheckListItem (list, pattern, QCheckListItem.Type.CheckBox);
			item.SetOn ((bool)enabled [i]);
		}
	}

	public void Show () {
		dialog.Show ();
	}
}
}	
