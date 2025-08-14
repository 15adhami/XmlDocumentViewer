using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    public class XmlDocumentViewer : Mod
    {
        internal static XmlDocument prePatchDocument = [];
        internal static XmlDocument postPatchDocument = [];
        internal static XmlDocument postInheritanceDocument = [];

        internal static List<XmlNode> nodeList = [];
        internal static bool shouldAddToDoc = false;

        private string xpath = "";
        private XmlNodeList prePatchList;
        private XmlNodeList postPatchList;
        private XmlNodeList postInheritanceList;
        private SelectedList selectedList = SelectedList.prePatch;

        private Vector2 scrollPrePatch = Vector2.zero;
        private Vector2 scrollPostPatch = Vector2.zero;
        private Vector2 scrollPostInheritance = Vector2.zero;
        private bool errorXpath = false;

        // For optimization
        private string outerXml = null;
        private List<string> lines = new();
        private float contentWidth = 0f;
        private float contentHeight = 0f;
        private float lineH = 0f;

        internal static float prePatchSize = 0;
        internal static float postPatchSize = 0;
        internal static float postInheritanceSize = 0;

        private float gapSize = 6f;


        private enum SelectedList
        {
            prePatch,
            postPatch,
            postInheritance
        }

        private XmlNodeList CurrentResults =>
            selectedList == SelectedList.prePatch ? prePatchList :
            selectedList == SelectedList.postPatch ? postPatchList : postInheritanceList;

        private XmlDocument CurrentDocument =>
            selectedList == SelectedList.prePatch ? prePatchDocument :
            selectedList == SelectedList.postPatch ? postPatchDocument : postInheritanceDocument;

        

        public XmlDocumentViewer(ModContentPack content) : base(content)
        {
            Harmony harmony = new("com.github.15adhami.xmldocumentviewer");
            harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new();
            listing.Begin(inRect);

            listing.Label("Enter XPath and select XmlDocument state:");
            xpath = listing.TextEntry(xpath);
            listing.Gap(gapSize);
            try
            {
                Rect xpathRect = listing.GetRect(28f);
                if (Widgets.ButtonText(xpathRect, "Search XPath"))
                {
                    prePatchList = prePatchDocument?.SelectNodes(xpath);
                    postPatchList = postPatchDocument?.SelectNodes(xpath);
                    postInheritanceList = postInheritanceDocument?.SelectNodes(xpath);
                    scrollPrePatch = scrollPostPatch = scrollPostInheritance = Vector2.zero;

                    XmlNode node = CurrentResults[0];
                    ResetRenderCache(node);
                    ComputeLineMetrics();
                }
                errorXpath = false;
            }
            catch
            {
                errorXpath = true;
                prePatchList = null;
                postPatchList = null;
                postInheritanceList = null;
            }

            listing.GapLine(gapSize * 2);

            Rect tabs = listing.GetRect(28f);
            float w = tabs.width / 3f;
            Rect r1 = new(tabs.x + 0 * w, tabs.y, w - 4f, 28f);
            Rect r2 = new(tabs.x + 1 * w, tabs.y, w - 4f, 28f);
            Rect r3 = new(tabs.x + 2 * w, tabs.y, w - 4f, 28f);

            if (selectedList == SelectedList.prePatch) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(r1, $"Before Patching ({prePatchSize:F2} MB total)"))
            {
                selectedList = SelectedList.prePatch;
                if (CurrentResults != null && CurrentResults[0] != null)
                { // TODO: Double check this condition
                    XmlNode node = CurrentResults[0];
                    ResetRenderCache(node);
                    ComputeLineMetrics();
                }
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(r1, "View the XmlDocument before any patch operations have been run.");

            if (selectedList == SelectedList.postPatch) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(r2, $"After Patching ({postPatchSize:F2} MB total)"))
            {
                selectedList = SelectedList.postPatch;
                if (CurrentResults != null && CurrentResults[0] != null)
                {
                    XmlNode node = CurrentResults[0];
                    ResetRenderCache(node);
                    ComputeLineMetrics();
                }
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(r2, "View the XmlDocument after all patch operations but before inheritance.");

            if (selectedList == SelectedList.postInheritance) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(r3, $"After Inheritance ({postInheritanceSize:F2} MB total)"))
            {
                selectedList = SelectedList.postInheritance;
                if (CurrentResults != null && CurrentResults[0] != null)
                {
                    XmlNode node = CurrentResults[0];
                    ResetRenderCache(node);
                    ComputeLineMetrics();
                }
            }
            
            GUI.color = Color.white;
            TooltipHandler.TipRegion(r3, "View the XmlDocument after inheritance.");
            listing.Gap(gapSize);

            XmlDocument doc = CurrentDocument;
            XmlNodeList results = CurrentResults;
            if (errorXpath)
            {
                listing.Label($"Invalid XPath.");
            }
            else if (doc == null)
            {
                listing.Label($"Selected document not available yet.");
            }
            else if (results == null)
            {
                listing.Label("Enter XPath and click \"Search XPath\".");
            }
            else if (results.Count == 0)
            {
                listing.Label($"No nodes found.");
            }
            else
            {
                // Set font
                GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor prevAnchor = Text.Anchor;
                Text.Font = GameFont.Tiny; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;

                if (lines.Count == 0) { listing.Label("No text found"); listing.End(); return; }

                // Draw section
                Rect sectionRect = listing.GetRect(inRect.height - listing.CurHeight - 4f);
                Widgets.DrawMenuSection(sectionRect);

                Rect outRect = sectionRect.ContractedBy(4f);
                float vrW = Mathf.Max(contentWidth, outRect.width - GenUI.ScrollBarWidth);
                float vrH = Mathf.Max(contentHeight, outRect.height - GenUI.ScrollBarWidth);

                // Draw ScrollView
                Rect scrollRect = new Rect(0, 0, vrW, vrH - 3*lineH);
                ref Vector2 scroll = ref CurrentScrollRef();
                Widgets.BeginScrollView(outRect, ref scroll, scrollRect);

                int n = lines.Count;

                float topY = scroll.y;
                float botY = topY + outRect.height - GenUI.ScrollBarWidth;

                int start = Mathf.Clamp((int)Mathf.Floor(topY / lineH), 0, n - 1);
                int end = Mathf.Clamp((int)Mathf.Ceil(botY / lineH) + 4, start, n - 1);

                string slice = string.Join("\n", lines.GetRange(start, end - start + 1));

                float yStart = start * lineH;
                float blockH = (end - start + 1) * lineH;

                float drawW = vrW;
                GUI.Label(new Rect(0f, yStart, drawW, blockH), slice);

                Text.Anchor = prevAnchor; Text.WordWrap = prevWrap; Text.Font = prevFont;

                Widgets.EndScrollView();

                // Copy button
                Rect copyRect = sectionRect.RightPartPixels(32f).BottomPartPixels(32f);
                float horButtonPadding = 12f + (contentHeight >= outRect.height ? GenUI.ScrollBarWidth : 0f);
                float vertButtonPadding = 12f + (contentWidth >= outRect.width ? GenUI.ScrollBarWidth : 0f);

                copyRect.position -= new Vector2(horButtonPadding, vertButtonPadding);
                GUI.DrawTexture(copyRect, TexButton.Copy);
                Widgets.DrawHighlightIfMouseover(copyRect);
                TooltipHandler.TipRegion(copyRect, "Copy to clipboard");
                if (Widgets.ButtonInvisible(copyRect))
                {
                    string plain = XmlRich.StripRichColorTags(outerXml);
                    GUIUtility.systemCopyBuffer = plain;
                    Messages.Message("Copied node to clipboard.", MessageTypeDefOf.TaskCompletion, historical: false);
                }
            }
            listing.End();
        }


        public override string SettingsCategory()
        {
            return "XmlDocument Viewer";
        }

        // Helper methods

        private void ResetRenderCache(XmlNode node)
        {
            outerXml = XmlRich.ColorizeXml(node, maxDepth: 64, maxChildrenPerNode: int.MaxValue, maxTextLen: int.MaxValue); ;
            lines.Clear();
            contentWidth = contentHeight = 0f;
        }

        private void ComputeLineMetrics()
        {
            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor prevAnchor = Text.Anchor;
            Text.Font = GameFont.Tiny; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;

            string[] split = outerXml.Split('\n');
            int len = split.Length;
            if (len > 0 && (split[len - 1].Length == 0)) len--;

            for (int i = 0; i < len; i++)
            {
                lines.Add(split[i]);
                Vector2 sz = Text.CalcSize(split[i]);
                if (sz.x > contentWidth) contentWidth = sz.x;
            }

            lineH = Text.LineHeight;
            contentHeight = lines.Count * lineH;

            Text.Anchor = prevAnchor; Text.WordWrap = prevWrap; Text.Font = prevFont;
        }

        private ref Vector2 CurrentScrollRef()
        {
            if (selectedList == SelectedList.prePatch) return ref scrollPrePatch;
            if (selectedList == SelectedList.postPatch) return ref scrollPostPatch;
            return ref scrollPostInheritance;
        }
    }
}
