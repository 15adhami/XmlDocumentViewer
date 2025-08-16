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
            if (XmlDocumentViewer_Mod.shouldAddToDoc && node != null)
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
                    XmlDocumentViewer_Mod.nodeList.Add(XmlDocumentViewer_Mod.postInheritanceDocument.ImportNode(resolvedNode, true));
                }
            }
        }
    }
}
