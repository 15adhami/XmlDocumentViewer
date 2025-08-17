using System.Collections.Generic;
using System.Xml;

namespace XmlDocumentViewer
{
    internal static class XmlNodeListExtensions
    {
        public static List<XmlNode> ToList(this XmlNodeList nodes)
        {
            List<XmlNode> list = [];
            foreach (XmlNode node in nodes)
            {
                if (node != null) list.Add(node);
            }
            return list;
        }
    }
}