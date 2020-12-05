using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

class XmlElement
{
	public string Name;
	public int Depth;
	public bool IsResultContainer;

	#region private
	internal bool Seen;
	#endregion
}

class XmlReaderWrapper
{
	public static List<Dictionary<string,string>> Parse(string filename, List<XmlElement> elements)
	{
		if (!File.Exists(filename)) throw new Exception($"File must exist : {filename}");
		if (elements == null || elements.Count == 0) throw new Exception("Must provide a valid list of elements to find");

		var results = new List<Dictionary<string,string>>();

		// read through the file and capture data based on the nodes in elements
		using (var reader = XmlReader.Create(filename))
		{
			// clear the seen bit
			foreach(var elem in elements) elem.Seen = false;

			// read
			XmlElement activeElement = null;
			Dictionary<string,string> current = null;
			while(reader.Read())
			{
				switch(reader.NodeType)
				{
				case XmlNodeType.Element:
					// check if this is an element that we care about
					foreach(var elem in elements)
					{
						if (elem.Seen) continue;
						// find a match for this element
						if (string.Equals(reader.Name, elem.Name, StringComparison.OrdinalIgnoreCase))
						{
							// found one, break early
							activeElement = elem;
							activeElement.Seen = true;
							break;
						}
					}

					// if this is a start of a result, create it
					if (activeElement != null && activeElement.IsResultContainer)
					{
						// create the new result
						current = new Dictionary<string,string>();
						activeElement = null;
					}
					break;
				case XmlNodeType.Text:
					if (activeElement != null)
					{
						// capture the text and remove the active element
						current.Add(activeElement.Name.ToLower(), reader.Value);
						activeElement = null;
					}
					break;
				case XmlNodeType.EndElement:
					// check if this is the end of a result
					var endResult = false;
					foreach(var elem in elements)
					{
						if (!elem.IsResultContainer) continue;
						// find a match for this element
						if (string.Equals(reader.Name, elem.Name, StringComparison.OrdinalIgnoreCase))
						{
							endResult = true;
							break;
						}
					}

					if (endResult)
					{
						// this is the end of the result container
						results.Add(current);
						current = null;
						activeElement = null;

						// clear the seen bit
						foreach(var elem in elements) elem.Seen = false;
					}
					break;
				case XmlNodeType.XmlDeclaration:
					// ignore
					break;
				default:
					throw new Exception($"Other node {reader.NodeType} with value {reader.Value}");
				}
			}
		}

		return results;
	}
}