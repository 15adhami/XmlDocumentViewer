using HarmonyLib;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace XmlDocumentViewer
{
    [HarmonyPatch(typeof(LoadedModManager), "ApplyPatches")]
    internal class LoadedModManager_ApplyPatches_Patch
    {
        private static void Prefix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup, List<ModContentPack> ___runningMods)
        {
            XmlDocumentViewer_Mod.prePatchDocument = (XmlDocument)xmlDoc.CloneNode(true);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(XmlDocumentViewer_Mod.prePatchDocument.OuterXml);
            XmlDocumentViewer_Mod.prePatchSize = bytes;
        }

        private static void Postfix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup)
        {
            XmlDocumentViewer_Mod.postPatchDocument = (XmlDocument)xmlDoc.CloneNode(true);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(XmlDocumentViewer_Mod.postPatchDocument.OuterXml);
            XmlDocumentViewer_Mod.postPatchSize = bytes;
        }
    }
}
