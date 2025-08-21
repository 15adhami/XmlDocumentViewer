using HarmonyLib;
using System.Xml;
using Verse;

namespace XmlDocumentViewer
{
    [HarmonyPatch(typeof(DirectXmlToObjectNew), "DefFromNodeNew")]
    internal class DirectXmlToObjectNew_DefFromNodeNew_Patch
    {
        private static void Prefix(XmlNode node, LoadableXmlAsset loadingAsset)
        {
            if (XmlDocumentManager.shouldAddToDoc && node != null)
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
                    XmlDocumentManager.nodeList.Add(XmlDocumentManager.postInheritanceDocument.ImportNode(resolvedNode, true));
                }
            }
        }
    }
}
