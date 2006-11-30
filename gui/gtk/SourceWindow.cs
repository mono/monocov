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
			SetDefaultSize (800, 600);

			ScrolledWindow sw = new ScrolledWindow ();
			
			text_buffer = new TextBuffer (new TextTagTable ());
			text_view = new TextView (text_buffer);
			text_view.Editable = false;

			sw.Add (text_view);
			Add (sw);

			hit_color = new TextTag ("hit");
			hit_color.Foreground = "blue";
			text_buffer.TagTable.Add (hit_color);
			missed_color = new TextTag ("miss");
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
				TextIter end = text_buffer.EndIter;
				text_buffer.Insert (ref end, String.Format ("{0, 6}  {1}\n", pos, infile.ReadLine ()));
				
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
			TextIter iter = text_buffer.GetIterAtLine (method.Model.startLine - 1);
			// this doesn't seem to work
			text_view.ScrollToIter (iter, 0.0, true, 0, 0.5);
			text_view.PlaceCursorOnscreen ();
			Console.WriteLine ("scrolled to line: {0}", method.Model.startLine - 1);
			// the first time we need to do this workaround for
			// it to actually work...
			while (Application.EventsPending ())
				Application.RunIteration ();
			text_view.ScrollToIter (iter, 0.0, true, 0, 0.5);
			text_view.PlaceCursorOnscreen ();
		}
	}
}
