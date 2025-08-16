using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace XmlDocumentViewer.Source
{
    [HarmonyPatch(typeof(LoadedModManager), "ParseAndProcessXML")]
    internal class LoadedModManager_ParseAndProcessXML_Patch
    {
        private static void Prefix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup, bool hotReload = false)
        {
            XmlDocumentViewer.shouldAddToDoc = true;
        }

        private static void Postfix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup, bool hotReload = false)
        {
            XmlDocumentViewer.shouldAddToDoc = false;
            XmlDeclaration decl = XmlDocumentViewer.postInheritanceDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlDocumentViewer.postInheritanceDocument.AppendChild(decl);

            XmlElement root = XmlDocumentViewer.postInheritanceDocument.CreateElement("Defs");
            XmlDocumentViewer.postInheritanceDocument.AppendChild(root);
            XmlNode rootNode = root;
            try
            {
                int c = 0;
                foreach (XmlNode node in XmlDocumentViewer.nodeList)
                {
                    c++;
                    if (node != null)
                    {
                        rootNode.AppendChild(node);

                    }
                }
                int bytes = System.Text.Encoding.UTF8.GetByteCount(XmlDocumentViewer.postInheritanceDocument.OuterXml);
                XmlDocumentViewer.postInheritanceSize = bytes / (1024f * 1024f);
                XmlDocumentViewer.nodeList.Clear();
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
            }
        }
    }
}
