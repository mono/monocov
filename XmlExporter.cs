
using System;
using System.Collections;
using System.Text;
using System.IO;
using System.Xml;

namespace MonoCov {

public class XmlExporter {

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
 
	public void Export (CoverageModel model) {

		this.model = model;

		if (model.hit + model.missed == 0)
			return;

		if (StyleSheet == null) {
			// Use default stylesheet
			using (StreamReader sr = new StreamReader (typeof (XmlExporter).Assembly.GetManifestResourceStream ("style.xsl"))) {
				using (StreamWriter sw = new StreamWriter (Path.Combine (DestinationDir, "style.xsl"))) {
					string line;
					while ((line = sr.ReadLine ()) != null)
						sw.WriteLine (line);
				}
			}
			using (Stream s = typeof (XmlExporter).Assembly.GetManifestResourceStream ("trans.gif")) {
				using (FileStream fs = new FileStream (Path.Combine (DestinationDir, "trans.gif"), FileMode.Create)) {
					byte[] buf = new byte[1024];
					int len = s.Read (buf, 0, buf.Length);
					fs.Write (buf, 0, len);
				}
			}

			StyleSheet = DefaultStyleSheet;
		}

		// Count items
		itemCount = 1 + model.Classes.Count + model.Namespaces.Count;
		itemsProcessed = 0;

		WriteProject ();
		WriteNamespaces ();
		WriteClasses ();
	}

	private void WriteStyleSheet () {
		// The standard says text/xml, while IE6 only understands text/xsl
		writer.WriteProcessingInstruction ("xml-stylesheet", "href=\"" + StyleSheet + "\" type=\"text/xsl\"");
	}

	private void WriteProject () {
		string fileName = Path.Combine (DestinationDir, "project.xml");

		// If I use Encoding.UTF8 here, the file will start with strange
		// characters
		writer = new XmlTextWriter (fileName, Encoding.ASCII);
		writer.Formatting = Formatting.Indented;
		writer.WriteStartDocument ();
		WriteStyleSheet ();

		WriteItem (model, typeof (ClassCoverageItem), 999);
		
		writer.WriteEndDocument ();
		writer.WriteRaw ("\n");
		writer.Close ();

		itemsProcessed ++;
		if (Progress != null)
			Progress (this, new ProgressEventArgs (model, fileName, itemsProcessed, itemCount));
	}

	private void WriteItem (CoverageItem item, Type stopLevel, int level) {
		if (item.filtered)
			return;

		if (item.hit + item.missed == 0)
			// Filtered
			return;

		if (level == 0)
			return;

		if (item.GetType () == stopLevel)
			return;

		if (item is CoverageModel) {
			writer.WriteStartElement ("project");
			writer.WriteAttributeString ("name", "Project");
		}
		else
			if (item is NamespaceCoverageItem) {
				NamespaceCoverageItem ns = (NamespaceCoverageItem)item;
				writer.WriteStartElement ("namespace");

				if (ns.ns == "<GLOBAL>")
					writer.WriteAttributeString ("name", "GLOBAL");
				else
					writer.WriteAttributeString ("name", ns.ns);
			}
		else
			if (item is ClassCoverageItem) {
				ClassCoverageItem klass = (ClassCoverageItem)item;
				writer.WriteStartElement ("class");
				writer.WriteAttributeString ("name", klass.name);
				writer.WriteAttributeString ("fullname", klass.FullName.Replace('/', '.'));
			}

		WriteCoverage (item);

		if (item.ChildCount > 0)
			foreach (CoverageItem child in item.children)
				WriteItem (child, stopLevel, level - 1);
		writer.WriteEndElement ();
	}

	private void WriteNamespaces () {
		foreach (NamespaceCoverageItem ns in model.Namespaces.Values) {
			bool filtered = false;

			string fileSuffix = ns.ns;
			if (ns.ns == "<GLOBAL>")
				fileSuffix = "GLOBAL";

			string fileName = 
				Path.Combine (DestinationDir, String.Format ("namespace-{0}.xml", fileSuffix));

			if (ns.hit + ns.missed == 0)
				// Filtered
				filtered = true;

			if (!filtered) {
				writer = new XmlTextWriter (fileName, Encoding.ASCII);
				writer.Formatting = Formatting.Indented;
				writer.WriteStartDocument ();
				WriteStyleSheet ();

				WriteItem (ns, typeof (MethodCoverageItem), 2);
		
				writer.WriteEndDocument ();
				writer.WriteRaw ("\n");
				writer.Close ();
			}
			else
				fileName = null;

			itemsProcessed ++;
			if (Progress != null)
				Progress (this, new ProgressEventArgs (ns, fileName, itemsProcessed, itemCount));
		}
	}

	private void WriteClasses () {
		foreach (ClassCoverageItem item in model.Classes.Values) {
			bool filtered = false;

			string fileName = Path.Combine (DestinationDir, String.Format ("class-{0}.xml", item.FullName.Replace('/', '.')));

			if (item.filtered)
				filtered = true;

			if (item.hit + item.missed == 0)
				// Filtered
				filtered = true;

			if (!filtered) {
				writer = new XmlTextWriter (fileName, Encoding.ASCII);
				writer.Formatting = Formatting.Indented;
				writer.WriteStartDocument ();
				WriteStyleSheet ();

				WriteClass (item);

				writer.WriteEndDocument ();
				writer.WriteRaw ("\n");
				writer.Close ();
			}
			else
				fileName = null;

			itemsProcessed ++;
			if (Progress != null)
				Progress (this, new ProgressEventArgs (item, fileName, itemsProcessed, itemCount));
		}
	}

	private void WriteClass (ClassCoverageItem item) {
		if (item.filtered)
			return;

		writer.WriteStartElement ("class");
		writer.WriteAttributeString ("name", item.name);
		writer.WriteAttributeString ("fullname", item.FullName.Replace('/', '.'));
		writer.WriteAttributeString ("namespace", item.Namespace);

		WriteCoverage (item);

		writer.WriteStartElement ("source");

		if (item.sourceFile != null) {
			writer.WriteAttributeString ("sourceFile", item.sourceFile.sourceFile);

			StreamReader infile = new StreamReader (item.sourceFile.sourceFile, Encoding.ASCII);
			int[] coverage = item.sourceFile.Coverage;
			int pos = 1;
			while (infile.Peek () > -1) {
				int count;
				if ((coverage != null) && (pos < coverage.Length))
					count = coverage [pos];
				else
					count = -1;
				writer.WriteStartElement ("l");
				writer.WriteAttributeString ("line", "" + pos);
				writer.WriteAttributeString ("count", "" + count);
				string line = infile.ReadLine ();
				writer.WriteString (line);
				writer.WriteEndElement ();
				
				pos ++;
			}
		}

		writer.WriteEndElement ();
	}

	private void WriteCoverage (CoverageItem item) {

		double coverage;
		if (item.hit + item.missed == 0)
			coverage = 1.0;
		else
			coverage = (double)item.hit / (item.hit + item.missed);

		string coveragePercent 
			= String.Format ("{0:###0}", coverage * 100);

		writer.WriteStartElement ("coverage");
		writer.WriteAttributeString ("hit", item.hit.ToString ());
		writer.WriteAttributeString ("missed", item.missed.ToString ());
		writer.WriteAttributeString ("coverage", coveragePercent);
		writer.WriteEndElement ();
	}
}
}
