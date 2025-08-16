using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace XmlDocumentViewer
{
    [HarmonyPatch(typeof(LoadedModManager), "ParseAndProcessXML")]
    internal class LoadedModManager_ParseAndProcessXML_Patch
    {
        private static void Prefix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup, bool hotReload = false)
        {
            XmlDocumentViewer_Mod.shouldAddToDoc = true;
        }

        private static void Postfix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup, bool hotReload = false)
        {
            XmlDocumentViewer_Mod.shouldAddToDoc = false;
            XmlDeclaration decl = XmlDocumentViewer_Mod.postInheritanceDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlDocumentViewer_Mod.postInheritanceDocument.AppendChild(decl);

            XmlElement root = XmlDocumentViewer_Mod.postInheritanceDocument.CreateElement("Defs");
            XmlDocumentViewer_Mod.postInheritanceDocument.AppendChild(root);
            XmlNode rootNode = root;
            try
            {
                int c = 0;
                foreach (XmlNode node in XmlDocumentViewer_Mod.nodeList)
                {
                    c++;
                    if (node != null)
                    {
                        rootNode.AppendChild(node);

                    }
                }
                int bytes = System.Text.Encoding.UTF8.GetByteCount(XmlDocumentViewer_Mod.postInheritanceDocument.OuterXml);
                XmlDocumentViewer_Mod.postInheritanceSize = bytes / (1024f * 1024f);
                XmlDocumentViewer_Mod.nodeList.Clear();
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
            }
        }
    }
}
