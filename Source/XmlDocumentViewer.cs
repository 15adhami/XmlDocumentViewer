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

        // Gutter cache
        private float prevUiScale = -1f;
        private float maxDigitW = 0f;
        private float spaceW = 0f;

        // Gutter visuals
        private static readonly Color GutterBg = new Color(0.14f, 0.14f, 0.14f, 1f); // dark slab
        private static readonly Color GutterSep = new Color(0.25f, 0.25f, 0.25f, 1f); // thin separator
        private static readonly Color LineNumFg = new Color(0.65f, 0.65f, 0.65f, 1f); // grey numbers

        // --- constants for gutter visuals ---
        const float sepW = 1f;  // separator thickness
        const float numRightPad = 4f;  // NEW: inner padding to the right of numbers


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
                // Section frame
                Rect sectionRect = listing.GetRect(inRect.height - listing.CurHeight - 4f);
                Widgets.DrawMenuSection(sectionRect);

                // Viewport split: fixed gutter, scrolling code area
                Rect outRect = sectionRect.ContractedBy(4f);
                EnsureGutterMetrics();
                int totalLines = lines.Count;
                int digits = Mathf.Max(2, totalLines > 0 ? (int)Mathf.Floor(Mathf.Log10(totalLines)) + 1 : 1);
                const float gutterPad = 3f;
                float gutterPx = digits * maxDigitW + spaceW + gutterPad;

                // fixed gutter + separator (does not scroll)
                Rect gutterRect = new Rect(outRect.x, outRect.y, gutterPx, outRect.height);
                Widgets.DrawBoxSolid(gutterRect, GutterBg);

                // draw the separator INSIDE the gutter so the code area starts cleanly
                Widgets.DrawBoxSolid(new Rect(gutterRect.xMax - sepW, outRect.y, sepW, outRect.height), GutterSep);

                // scrolling code viewport (everything to the right of gutter)
                Rect codeViewport = new Rect(outRect.x + gutterPx, outRect.y, outRect.width - gutterPx, outRect.height);

                float codePadding = 2f;
                float vrW = Mathf.Max(contentWidth + GenUI.ScrollBarWidth + codePadding, codeViewport.width - GenUI.ScrollBarWidth);
                float vrH = Mathf.Max(contentHeight + GenUI.ScrollBarWidth + codePadding, codeViewport.height - GenUI.ScrollBarWidth);

                Rect viewRect = new Rect(0f, 0f, vrW, vrH);
                ref Vector2 scroll = ref CurrentScrollRef();
                Widgets.BeginScrollView(codeViewport, ref scroll, viewRect);

                // visible slice
                float topY = scroll.y;
                float botY = topY + codeViewport.height - GenUI.ScrollBarWidth;
                int start = Mathf.Clamp((int)Mathf.Floor(topY / lineH), 0, totalLines - 1);
                int end = Mathf.Clamp((int)Mathf.Ceil(botY / lineH), start, totalLines - 1);

                // build strings
                var numsSb = new StringBuilder((end - start + 1) * (digits + 1));
                var txtSb = new StringBuilder((end - start + 1) * 64);
                for (int i = start; i <= end; i++)
                {
                    if (i < end) numsSb.Append(i + 1); numsSb.Append('\n');
                    txtSb.Append(lines[i]); if (i < end) txtSb.Append('\n');
                }
                string numsSlice = numsSb.ToString();
                string textSlice = txtSb.ToString();

                // draw code inside the scrollview
                float yStart = start * lineH;
                float blockH = (end - start + 1) * lineH;

                var pf = Text.Font; var pw = Text.WordWrap; var pa = Text.Anchor; var pc = GUI.color;
                Text.Font = GameFont.Tiny; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, yStart, contentWidth + GenUI.ScrollBarWidth + codePadding, blockH + GenUI.ScrollBarWidth + codePadding), textSlice, Text.CurFontStyle);
                Widgets.EndScrollView();

                // draw line numbers fixed (outside the scrollview), aligned with scroll
                GUI.BeginGroup(outRect);                // local coords
                Text.Anchor = TextAnchor.UpperRight;
                GUI.color = LineNumFg;
                float yStartFixed = yStart - scroll.y;
                float numRectW = gutterPx - sepW - numRightPad;  // keep space before the separator
                //GUI.Label(new Rect(0f, yStartFixed, numRectW, blockH + (contentHeight > codeViewport.height - GenUI.ScrollBarWidth ? GenUI.ScrollBarWidth + codePadding : 0f)), numsSlice, Text.CurFontStyle);
                GUI.Label(new Rect(0f, yStartFixed, numRectW, codeViewport.height + GenUI.ScrollBarWidth), numsSlice, Text.CurFontStyle);
                GUI.EndGroup();

                // restore
                GUI.color = pc; Text.Anchor = pa; Text.WordWrap = pw; Text.Font = pf;

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
                    plain = XmlRich.NormalizeIndentForCopy(plain, from: XmlRich.INDENT, to: 4);
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
            Text.Font = GameFont.Tiny; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;

            lines.Clear();
            contentWidth = contentHeight = 0f;
            outerXml = XmlRich.ColorizeXml(node, maxDepth: 64, maxChildrenPerNode: int.MaxValue, maxTextLen: int.MaxValue);
            string[] split = outerXml.Split('\n');
            int len = split.Length;
            while (split[len - 1].Length == 0 || split.GetLast().Length == 1) len--;
            lineH = Text.CurFontStyle.font.lineHeight;
            int maxLength = 0;
            foreach (string line in split)
            {
                lines.Add(line);
                if (line.Length * (lineH / 2f) > contentWidth)
                {
                    Vector2 sz = Text.CalcSize(line);
                    if (sz.x > contentWidth) contentWidth = sz.x;
                }
            }
            contentHeight = lines.Count * lineH;

            Text.Anchor = prevAnchor; Text.WordWrap = prevWrap; Text.Font = prevFont;
        }

        private void EnsureGutterMetrics()
        {
            float ui = Prefs.UIScale;
            if (maxDigitW > 0f && Mathf.Approximately(prevUiScale, ui)) return;

            var pf = Text.Font; var pw = Text.WordWrap;
            Text.Font = GameFont.Tiny; Text.WordWrap = false;

            maxDigitW = 0f;
            for (char c = '0'; c <= '9'; c++)
                maxDigitW = Mathf.Max(maxDigitW, Text.CalcSize(c.ToString()).x);
            spaceW = Text.CalcSize(" ").x;

            Text.WordWrap = pw; Text.Font = pf;
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
