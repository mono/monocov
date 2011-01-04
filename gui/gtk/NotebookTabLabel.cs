
using System;
using Gtk;
using Gdk;

namespace MonoCov
{
	public class NotebookTabLabel : EventBox
	{
		public NotebookTabLabel (string title)
		{
			Button button = new Button ();
			button.Image = new Gtk.Image (Stock.Close, IconSize.Menu);
			button.TooltipText = "Close Tab";
			button.Relief = ReliefStyle.None;
			
			RcStyle rcStyle = new RcStyle ();
			rcStyle.Xthickness = 0;
			rcStyle.Ythickness = 0;
			button.ModifyStyle (rcStyle);
			
			button.FocusOnClick = false;
			button.Clicked += OnCloseClicked;
			button.Show ();
			
			Label label = new Label (title);
			label.UseMarkup = false;
			label.UseUnderline = false;
			label.Show ();
			
			HBox hbox = new HBox (false, 0);
			hbox.Spacing = 0;
			hbox.Add (label);
			hbox.Add (button);
			hbox.Show ();
			
			this.Add (hbox);
		}

		public event EventHandler<EventArgs> CloseClicked;

		public void OnCloseClicked (object sender, EventArgs e)
		{
			if (CloseClicked != null)
				CloseClicked (sender, e);
		}

		public void OnCloseClicked ()
		{
			OnCloseClicked (this, EventArgs.Empty);
		}
	}
}

