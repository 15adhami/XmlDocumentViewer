using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace XmlDocumentViewer
{
    public class XmlDocumentViewer : Mod
    {
        // Menu visuals
        private readonly GameFont codeFont = GameFont.Small;
        private readonly float gapSize = 6f;

        // Gutter visuals
        private static readonly Color GutterBackgroundColor = new(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color GutterSeparatorColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color LineNumberColor = new(0.65f, 0.65f, 0.65f, 1f);
        const float gutterSeparatorThickness = 1f;
        const float lineNumberRightPadding = 4f;

        // XmlDocuments
        internal static XmlDocument prePatchDocument = [];
        internal static XmlDocument postPatchDocument = [];
        internal static XmlDocument postInheritanceDocument = [];
        internal static float prePatchSize = 0;
        internal static float postPatchSize = 0;
        internal static float postInheritanceSize = 0;

        // Temporary variables to create postInheritanceDocument
        internal static List<XmlNode> nodeList = [];
        internal static bool shouldAddToDoc = false;

        // Private menu fields
        private string xpath = "";
        private XmlNodeList prePatchList;
        private XmlNodeList postPatchList;
        private XmlNodeList postInheritanceList;
        private SelectedList selectedList = SelectedList.prePatch;
        private Vector2 scrollPrePatch = Vector2.zero;
        private Vector2 scrollPostPatch = Vector2.zero;
        private Vector2 scrollPostInheritance = Vector2.zero;
        private bool errorXpath = false;

        // Caches
        private readonly float cullingHeuristicRatio = 0.5f;
        private readonly List<string> lines = [];
        private string outerXml = null;
        private float contentWidth = 0f;
        private float contentHeight = 0f;
        private float lineHeight = 0f;

        // Gutter caches
        private float prevUiScale = -1f;
        private float maxDigitWidth = 0f;
        private float spaceWidth = 0f;

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

            // Draw XPath Search
            listing.Label("Enter XPath and select XmlDocument state:");
            xpath = listing.TextEntry(xpath);
            listing.Gap(gapSize);
            try
            {
                Rect xpathRect = listing.GetRect(28f);
                if (Widgets.ButtonText(xpathRect, "Search XPath"))
                {
                    prePatchList = prePatchDocument.SelectNodes(xpath);
                    postPatchList = postPatchDocument.SelectNodes(xpath);
                    postInheritanceList = postInheritanceDocument.SelectNodes(xpath);
                    scrollPrePatch = scrollPostPatch = scrollPostInheritance = Vector2.zero;

                    if (CurrentResults != null && CurrentResults[0] != null)
                    {
                        XmlNode node = CurrentResults[0];
                        ComputeLineMetrics(node);
                    }
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

            // Draw XmlDocument buttons
            Rect buttonsRect = listing.GetRect(28f);
            float buttonWidth = buttonsRect.width / 3f;
            Rect r1 = new(buttonsRect.x + 0 * buttonWidth, buttonsRect.y, buttonWidth - 4f, 28f);
            Rect r2 = new(buttonsRect.x + 1 * buttonWidth, buttonsRect.y, buttonWidth - 4f, 28f);
            Rect r3 = new(buttonsRect.x + 2 * buttonWidth, buttonsRect.y, buttonWidth - 4f, 28f);

            if (selectedList == SelectedList.prePatch) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(r1, $"Before Patching ({prePatchSize:F2} MB total)"))
            {
                selectedList = SelectedList.prePatch;
                if (CurrentResults != null && CurrentResults[0] != null)
                {
                    XmlNode node = CurrentResults[0];
                    ComputeLineMetrics(node);
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
                    ComputeLineMetrics(node);
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
                    ComputeLineMetrics(node);
                }
            }
            
            GUI.color = Color.white;
            TooltipHandler.TipRegion(r3, "View the XmlDocument after inheritance.");
            listing.Gap(gapSize);

            // Draw xml
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
                Rect sectionRect = listing.GetRect(inRect.height - listing.CurHeight - 4f);
                Widgets.DrawMenuSection(sectionRect);

                // Viewport split
                Rect outRect = sectionRect.ContractedBy(4f);
                ComputeGutterMetrics();
                int totalLines = lines.Count;
                int lineNumberDigits = Mathf.Max(2, totalLines > 0 ? (int)Mathf.Floor(Mathf.Log10(totalLines)) + 1 : 1);
                const float gutterPad = 3f;
                float gutterSize = lineNumberDigits * maxDigitWidth + spaceWidth + gutterPad;

                // Draw gutter outside the ScrollView
                Rect gutterRect = new(outRect.x, outRect.y, gutterSize, outRect.height);
                Widgets.DrawBoxSolid(gutterRect, GutterBackgroundColor);

                // Draw gutter separator
                Widgets.DrawBoxSolid(new Rect(gutterRect.xMax - gutterSeparatorThickness, outRect.y, gutterSeparatorThickness, outRect.height), GutterSeparatorColor);

                // Viewport
                float codePadding = 2f;
                Rect codeViewRect = new Rect(outRect.x + gutterSize, outRect.y, outRect.width - gutterSize, outRect.height);
                float viewRectWidth = Mathf.Max(contentWidth + GenUI.ScrollBarWidth + codePadding, codeViewRect.width - GenUI.ScrollBarWidth);
                float viewRectHeight = Mathf.Max(contentHeight + GenUI.ScrollBarWidth + codePadding, codeViewRect.height - GenUI.ScrollBarWidth);

                Rect viewRect = new(0f, 0f, viewRectWidth, viewRectHeight);
                ref Vector2 scroll = ref CurrentScrollRef();
                Widgets.BeginScrollView(codeViewRect, ref scroll, viewRect);

                // Visible slice
                float topY = scroll.y;
                float botY = topY + codeViewRect.height - GenUI.ScrollBarWidth;
                int startLineIndex = Mathf.Clamp((int)Mathf.Floor(topY / lineHeight), 0, totalLines - 1);
                int endLineIndex = Mathf.Clamp((int)Mathf.Ceil(botY / lineHeight) + 1, startLineIndex, totalLines - 1);

                // Build strings
                StringBuilder lineNumberStringBuilder = new((endLineIndex - startLineIndex + 1) * (lineNumberDigits + 1));
                StringBuilder codeStringBuilder = new((endLineIndex - startLineIndex + 1) * 64);
                for (int i = startLineIndex; i <= endLineIndex; i++)
                {
                    if (i < endLineIndex) lineNumberStringBuilder.Append(i + 1); lineNumberStringBuilder.Append('\n');
                    codeStringBuilder.Append(lines[i]); if (i < endLineIndex) codeStringBuilder.Append('\n');
                }
                string numsSlice = lineNumberStringBuilder.ToString();
                string textSlice = codeStringBuilder.ToString();

                // Draw code inside the scrollview
                float yStart = startLineIndex * lineHeight;
                float blockH = (endLineIndex - startLineIndex + 1) * lineHeight;

                GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor pervAnchor = Text.Anchor; Color prevColor = GUI.color;
                Text.Font = codeFont; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, yStart, contentWidth + GenUI.ScrollBarWidth + codePadding, blockH + GenUI.ScrollBarWidth + codePadding), textSlice, Text.CurFontStyle);
                Widgets.EndScrollView();

                // Draw line numbers outside the scrollview
                GUI.BeginGroup(outRect);
                Text.Anchor = TextAnchor.UpperRight;
                GUI.color = LineNumberColor;
                float yStartFixed = yStart - scroll.y;
                float lineNumberRectWidth = gutterSize - gutterSeparatorThickness - lineNumberRightPadding;
                GUI.Label(new Rect(0f, yStartFixed, lineNumberRectWidth, codeViewRect.height + Mathf.Max(GenUI.ScrollBarWidth, lineHeight)), numsSlice, Text.CurFontStyle);
                GUI.EndGroup();

                GUI.color = prevColor; Text.Anchor = pervAnchor; Text.WordWrap = prevWrap; Text.Font = prevFont;

                // Draw copy button
                Rect copyRect = sectionRect.RightPartPixels(32f).BottomPartPixels(32f);
                float horButtonPadding = 12f + (contentHeight >= outRect.height ? GenUI.ScrollBarWidth : 0f);
                float vertButtonPadding = 12f + (contentWidth >= outRect.width ? GenUI.ScrollBarWidth : 0f);

                copyRect.position -= new Vector2(horButtonPadding, vertButtonPadding);
                GUI.DrawTexture(copyRect, TexButton.Copy);
                Widgets.DrawHighlightIfMouseover(copyRect);
                TooltipHandler.TipRegion(copyRect, "Copy to clipboard");
                if (Widgets.ButtonInvisible(copyRect))
                {
                    string plain = RichXml.StripColorTags(outerXml);
                    plain = RichXml.PrepareIndentForCopy(plain);
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

        private void ComputeLineMetrics(XmlNode node)
        {
            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor prevAnchor = Text.Anchor;
            Text.Font = codeFont; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;

            lines.Clear();
            contentWidth = contentHeight = 0f;
            outerXml = RichXml.ColorizeXml(node);
            string[] split = outerXml.Split('\n');
            int len = split.Length;
            while (split[len - 1].Length == 0 || split.GetLast().Length == 1) len--;
            //lineHeight = Text.CurFontStyle.codeFont.lineHeight;
            lineHeight = Text.CurFontStyle.lineHeight;
            foreach (string line in split)
            {
                lines.Add(line);
                if (line.Length * (lineHeight * cullingHeuristicRatio) > contentWidth)
                {
                    Vector2 sz = Text.CalcSize(line);
                    if (sz.x > contentWidth) contentWidth = sz.x;
                }
            }
            contentHeight = lines.Count * lineHeight;

            Text.Anchor = prevAnchor; Text.WordWrap = prevWrap; Text.Font = prevFont;
        }

        private void ComputeGutterMetrics()
        {
            float ui = Prefs.UIScale;
            if (maxDigitWidth > 0f && Mathf.Approximately(prevUiScale, ui)) { return; }

            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap;
            Text.Font = codeFont; Text.WordWrap = false;

            maxDigitWidth = 0f;
            for (char c = '0'; c <= '9'; c++)
                maxDigitWidth = Mathf.Max(maxDigitWidth, Text.CalcSize(c.ToString()).x);
            spaceWidth = Text.CalcSize(" ").x;

            Text.WordWrap = prevWrap; Text.Font = prevFont;
            prevUiScale = ui;
        }

        private ref Vector2 CurrentScrollRef()
        {
            if (selectedList == SelectedList.prePatch) return ref scrollPrePatch;
            if (selectedList == SelectedList.postPatch) return ref scrollPostPatch;
            return ref scrollPostInheritance;
        }
    }
}
