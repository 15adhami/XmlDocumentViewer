using HarmonyLib;
using System.Xml;
using Verse;

namespace XmlDocumentViewer.Source
{
    [HarmonyPatch(typeof(DirectXmlToObjectNew), "DefFromNodeNew")]
    internal class DirectXmlToObjectNew_DefFromNodeNew_Patch
    {
        private static void Prefix(XmlNode node, LoadableXmlAsset loadingAsset)
        {
            if (XmlDocumentViewer.shouldAddToDoc && node != null)
            {
                XmlNode resolvedNode;
                try
                {
                    resolvedNode = XmlInheritance.GetResolvedNodeFor(node);
                }
                catch
                {
                    resolvedNode = node;
                }
                if (resolvedNode != null)
                {
                    XmlDocumentViewer.nodeList.Add(XmlDocumentViewer.postInheritanceDocument.ImportNode(resolvedNode, true));
                }
            }
        }
    }
}
