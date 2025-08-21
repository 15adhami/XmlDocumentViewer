using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace XmlDocumentViewer
{
    public static class XmlDocumentManager
    {
        // XmlDocuments
        public static XmlDocument prePatchDocument = [];
        public static XmlDocument postPatchDocument = [];
        public static XmlDocument postInheritanceDocument = [];
        public static int prePatchSize = 0;
        public static int postPatchSize = 0;
        public static int postInheritanceSize = 0;

        // Temporary variables to create postInheritanceDocument
        internal static List<XmlNode> nodeList = [];
        internal static bool shouldAddToDoc = false;

        public static int ComputeByteCount(XmlNodeList nodes)
        {
            int count = 0;
            if (nodes == null) return count;
            foreach (XmlNode node in nodes)
                count += Encoding.UTF8.GetByteCount(node?.OuterXml);
            return count;
        }
    }
}
