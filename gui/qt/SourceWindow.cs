
using Qt;
using System;
using System.Collections;
using System.IO;

namespace MonoCov.Gui.Qt {

public class SourceWindow : QWidget {

	protected QTextEdit editor;

	protected QPopupMenu contextMenu;

	public SourceWindow (QWidget parent, ClassItem klass) : 
		base (parent, "", WidgetFlags.WType_TopLevel) {
		SetCaption (klass.Model.sourceFile.sourceFile);
		Resize (640, 480);

		QBoxLayout layout = 
			new QBoxLayout (this, 0);

		editor = new QTextEdit (this);
		editor.SetTextFormat (TextFormat.PlainText);
		editor.SetReadOnly (true);
		editor.SetFamily ("misc-fixed");
		editor.SetPointSize (13);
		editor.SetWordWrap (QTextEdit.WordWrap.NoWrap);
		layout.AddWidget (editor);

		int[] coverage = klass.Model.sourceFile.Coverage;

		StreamReader infile = new StreamReader (klass.Model.sourceFile.sourceFile);
		int pos = 1;
		QColor deadColor = editor.Color ();
		QColor hitColor = new QColor ("blue");
		QColor missedColor = new QColor ("red");
		while (infile.Peek () > -1) {
			if (pos < coverage.Length) {
				int count = coverage [pos];
				if (count > 0)
					editor.SetColor (hitColor);
				else if (count == 0)
					editor.SetColor (missedColor);
				else
					editor.SetColor (deadColor);
			}
			else
				editor.SetColor (deadColor);
			editor.Append (String.Format ("{0, 6}", pos) + "  " + infile.ReadLine ());
			pos ++;
		}
		editor.SetCursorPosition (0, 0);
	}

	public void CenterOnMethod (MethodItem method) {
		// TODO: center the given method on the screen
		editor.SetCursorPosition (method.Model.startLine - 1, 0);
		editor.EnsureCursorVisible ();
		editor.MoveCursor (QTextEdit.CursorAction.MoveUp, false);
	}
}
}
