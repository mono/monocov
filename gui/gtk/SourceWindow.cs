using Gtk;
using System;
using System.IO;

namespace MonoCov.Gui.Gtk{

	public class SourceWindow : Window {
		TextView text_view;
		TextBuffer text_buffer;
		TextTag hit_color;
		TextTag missed_color;
		
		public SourceWindow (CoverageView.ClassItem klass)
			: base (klass.Model.sourceFile.sourceFile)
		{
			SetDefaultSize (640, 480);

			ScrolledWindow sw = new ScrolledWindow ();
			
			text_buffer = new TextBuffer (new Gtk.TextTagTable ());
			text_view = new TextView (text_buffer);
			text_view.Editable = false;

			sw.Add (text_view);
			Add (sw);

			hit_color = new Gtk.TextTag ("hit");
			hit_color.Foreground = "blue";
			text_buffer.TagTable.Add (hit_color);
			missed_color = new Gtk.TextTag ("miss");
			missed_color.Foreground = "red";
			text_buffer.TagTable.Add (missed_color);
			LoadFile (klass);
			ShowAll ();
		}

		void LoadFile (CoverageView.ClassItem klass)
		{
			int[] coverage = klass.Model.sourceFile.Coverage;

			StreamReader infile = new StreamReader (klass.Model.sourceFile.sourceFile);
			int pos = 1;

			while (infile.Peek () > -1) {
				int line = text_buffer.EndIter.Line;
				text_buffer.Insert (text_buffer.EndIter, String.Format ("{0, 6}  {1}\n", pos, infile.ReadLine ()));
				
				if (pos < coverage.Length) {
					int count = coverage [pos];
					TextIter text_end = text_buffer.EndIter;
					TextIter text_start = text_end;
					text_start.BackwardLines (1);
					
					if (count > 0)
						text_buffer.ApplyTag (hit_color, text_start, text_end);
					else if (count == 0)
						text_buffer.ApplyTag (missed_color, text_start, text_end);
				}
				pos++;
			}
		}

		public void CenterOnMethod (CoverageView.MethodItem method) {
			// TODO: center the given method on the screen
			//editor.SetCursorPosition (method.Model.startLine - 1, 0);
			//editor.EnsureCursorVisible ();
			//editor.MoveCursor (QTextEdit.CursorAction.MoveUp, false);
		}
	}
}
