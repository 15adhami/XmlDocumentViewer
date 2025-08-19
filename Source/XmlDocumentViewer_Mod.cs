using HarmonyLib;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    public class XmlDocumentViewer_Mod : Mod
    {
        // XmlDocuments
        public static XmlDocument prePatchDocument = [];
        public static XmlDocument postPatchDocument = [];
        public static XmlDocument postInheritanceDocument = [];
        public static int prePatchSize = 0;
        public static int postPatchSize = 0;
        public static int postInheritanceSize = 0;

        // Temporary variables to create postInheritanceDocument
        internal static List<XmlNode> nodeList = [];
        internal static bool shouldAddToDoc = false;

        private Vector2 xmlViewerButtonSize = new(256f, 48f);

        public XmlDocumentViewer_Mod(ModContentPack content) : base(content)
        {
            Harmony harmony = new("com.github.15adhami.xmldocumentviewer");
            harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            GUI.BeginGroup(inRect);
            Rect fullRect = new(0f, 0f, inRect.width, inRect.height);

            // Draw XmlDocument Viewer button
            float buttonPadding = 6f;
            Rect xmlViewerButtonSectionRect = fullRect.MiddlePartPixels(xmlViewerButtonSize.x + 2 * buttonPadding, fullRect.height).TopPartPixels(xmlViewerButtonSize.y + 2 * buttonPadding);
             
            // Draw background
            CustomWidgets.DrawColoredSection(xmlViewerButtonSectionRect, Dialog_XmlDocumentViewer.viewportColor);

            Rect xmlViewerButtonRect = xmlViewerButtonSectionRect.ContractedBy(buttonPadding);
            GUI.color = Color.white;// xmlViewerButtonColor;
            if (Widgets.ButtonText(xmlViewerButtonRect, "Open XmlDocument Viewer"))
                Find.WindowStack.Add(new Dialog_XmlDocumentViewer());
            GUI.color = Color.white;

            Rect settingsSectionRect = new(0f, xmlViewerButtonSectionRect.yMax + 16f, fullRect.width, fullRect.height - xmlViewerButtonSectionRect.yMax - 20f);
            settingsSectionRect.y -= 4f;
            CustomWidgets.DrawColoredSection(settingsSectionRect, Dialog_XmlDocumentViewer.viewportColor);
            Rect settingsFullRect = settingsSectionRect.ContractedBy(buttonPadding);

            // Draw settings header
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Rect settingsLabelRect = settingsFullRect.TopPartPixels(Text.LineHeight);
            Widgets.Label(settingsLabelRect, "Settings:");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Draw Settings
            Rect settingsRect = settingsFullRect.BottomPartPixels(settingsFullRect.height - settingsLabelRect.height + 4f);
            Widgets.Label(settingsRect, "Not yet available.");

            // Options: Condensed view; colors; 

            GUI.EndGroup();
        }


        public override string SettingsCategory()
        {
            return "XmlDocument Viewer";
        }
    }
}
