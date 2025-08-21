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
            XmlDocumentManager.shouldAddToDoc = true;
        }

        private static void Postfix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup, bool hotReload = false)
        {
            XmlDocumentManager.shouldAddToDoc = false;
            XmlDeclaration decl = XmlDocumentManager.postInheritanceDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlDocumentManager.postInheritanceDocument.AppendChild(decl);

            XmlElement root = XmlDocumentManager.postInheritanceDocument.CreateElement("Defs");
            XmlDocumentManager.postInheritanceDocument.AppendChild(root);
            XmlNode rootNode = root;
            try
            {
                int c = 0;
                foreach (XmlNode node in XmlDocumentManager.nodeList)
                {
                    c++;
                    if (node != null)
                    {
                        rootNode.AppendChild(node);

                    }
                }
                int bytes = System.Text.Encoding.UTF8.GetByteCount(XmlDocumentManager.postInheritanceDocument.OuterXml);
                XmlDocumentManager.postInheritanceSize = bytes;
                XmlDocumentManager.nodeList.Clear();
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
            }
        }
    }
}
