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
            XmlDocumentManager.prePatchDocument = (XmlDocument)xmlDoc.CloneNode(true);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(XmlDocumentManager.prePatchDocument.OuterXml);
            XmlDocumentManager.prePatchSize = bytes;
        }

        private static void Postfix(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup)
        {
            XmlDocumentManager.postPatchDocument = (XmlDocument)xmlDoc.CloneNode(true);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(XmlDocumentManager.postPatchDocument.OuterXml);
            XmlDocumentManager.postPatchSize = bytes;
        }
    }
}
