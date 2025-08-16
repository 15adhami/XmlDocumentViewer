using HarmonyLib;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using Verse;
using XmlDocumentViewer.Source;

namespace XmlDocumentViewer
{
    public class XmlDocumentViewer : Mod
    {
        // XmlDocuments
        public static XmlDocument prePatchDocument = [];
        public static XmlDocument postPatchDocument = [];
        public static XmlDocument postInheritanceDocument = [];
        public static float prePatchSize = 0;
        public static float postPatchSize = 0;
        public static float postInheritanceSize = 0;

        // Temporary variables to create postInheritanceDocument
        internal static List<XmlNode> nodeList = [];
        internal static bool shouldAddToDoc = false;

        private Color xmlViewerButtonColor = new(40 / 255f, 180 / 255f, 40 / 255f);

        public XmlDocumentViewer(ModContentPack content) : base(content)
        {
            Harmony harmony = new("com.github.15adhami.xmldocumentviewer");
            harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new() { verticalSpacing = 0f };
            listing.Begin(inRect);
            GUI.color = xmlViewerButtonColor;
            if (listing.ButtonText("Open XmlDocument Viewer"))
                Find.WindowStack.Add(new Dialog_XmlDocumentViewer());
            GUI.color = Color.white;
            listing.End();
        }


        public override string SettingsCategory()
        {
            return "XmlDocument Viewer";
        }
    }
}
