
using System;
using System.Collections;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;

namespace MonoCov {

public class HtmlExporter {

	public class ProgressEventArgs {
		public CoverageItem item;

		public string fileName;

		public int pos;

		public int itemCount;

		public ProgressEventArgs (CoverageItem item, string fileName, 
								  int pos, int itemCount) {
			this.item = item;
			this.fileName = fileName;
			this.pos = pos;
			this.itemCount = itemCount;
		}
	}

	public delegate void ProgressEventHandler (Object sender,
											   ProgressEventArgs e);


	public string DestinationDir;

	public string StyleSheet;

	public event ProgressEventHandler Progress;

	private XmlTextWriter writer;

	private CoverageModel model;

	private static string DefaultStyleSheet = "style.xsl";

	private int itemCount;

	private int itemsProcessed;

	private XslTransform transform;

	//
	// Algorithm: export the data as XML, then transform it into HTML
	// using a stylesheet
	//
 
	public void Export (CoverageModel model) {

		this.model = model;

		if (model.hit + model.missed == 0)
			return;

		// Why doesn't NET has a Path.GetTempDirectoryName () method ?
		int index = 0;
		string tempDir;
		// Of course this is not safe but it doesn't matter
		while (true) {
			tempDir = Path.Combine (Path.Combine (Path.GetTempPath (), "monocov"), "" + index);
			if (! Directory.Exists (tempDir))
				break;
			index ++;
		}
		Directory.CreateDirectory (tempDir);

		XmlExporter exporter = new XmlExporter ();
		exporter.StyleSheet = StyleSheet;
		exporter.DestinationDir = tempDir;
		exporter.Progress += new XmlExporter.ProgressEventHandler (progressListener);
		// Count items
		itemCount = 1 + model.Classes.Count + model.Namespaces.Count;
		itemsProcessed = 0;

		exporter.Export (model);

		Directory.Delete (tempDir, true);
	}

	private void progressListener (object sender, XmlExporter.ProgressEventArgs e) {
		if (e.fileName != null) {
			string name = Path.GetFileName (e.fileName);

			if (transform == null) {
				transform = new XslTransform();
				transform.Load(Path.Combine (Path.GetDirectoryName (e.fileName), "style.xsl"));
			}

			try {
				XsltArgumentList args = new XsltArgumentList ();
				args.AddParam ("link-suffix", "", ".html");
				transform.Transform (new XPathDocument (new StreamReader (e.fileName)), args, 
					new StreamWriter (Path.Combine (DestinationDir, name.Replace (".xml", ".html"))), null);
			}
			catch (Exception ex) {
				Console.WriteLine ("Error: Unable to transform '" + e.fileName + "': " + ex);
			}
		}

		if (Progress != null)
			Progress (this, new ProgressEventArgs (e.item, e.fileName, e.pos, e.itemCount));
	}	
}
}
