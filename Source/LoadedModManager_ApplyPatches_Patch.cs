using HarmonyLib;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace XmlDocumentViewer.Source
{
    [HarmonyPatch(typeof(LoadedModManager), "ApplyPatches")]
    internal class LoadedModManager_ApplyPatches_Patch
    {
        private static void Prefix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup, List<ModContentPack> ___runningMods)
        {
            XmlDocumentViewer.prePatchDocument = (XmlDocument)xmlDoc.CloneNode(true);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(XmlDocumentViewer.prePatchDocument.OuterXml);
            XmlDocumentViewer.prePatchSize = bytes / (1024f * 1024f);
        }

        private static void Postfix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup)
        {
            XmlDocumentViewer.postPatchDocument = (XmlDocument)xmlDoc.CloneNode(true);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(XmlDocumentViewer.postPatchDocument.OuterXml);
            XmlDocumentViewer.postPatchSize = bytes / (1024f * 1024f);
        }
    }
}
