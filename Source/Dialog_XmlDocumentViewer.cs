using RimWorld;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    internal class Dialog_XmlDocumentViewer : Window
    {
        // Menu visuals
        public override Vector2 InitialSize => new(Mathf.Min((float)UI.screenWidth * 0.9f, 1200f), Mathf.Min((float)UI.screenHeight * 0.9f, 900f));
        private readonly GameFont codeFont = GameFont.Small;
        private readonly float gapSize = 4f;
        private readonly float buttonHeight = 24f;
        private readonly Color xpathTipColor = new(1f, 1f, 1f, 0.5f);
        private readonly float codeViewportRatio = 0.66f;

        // Gutter visuals
        private static readonly Color GutterBackgroundColor = new(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color GutterSeparatorColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color LineNumberColor = new(0.65f, 0.65f, 0.65f, 1f);
        const float gutterSeparatorThickness = 1f;
        const float lineNumberRightPadding = 4f;

        // Private menu fields
        private string xpath = "";
        private XmlNodeList prePatchList;
        private XmlNodeList postPatchList;
        private XmlNodeList postInheritanceList;
        private SelectedList selectedList = SelectedList.prePatch;
        private Vector2 scrollPrePatch = Vector2.zero;
        private Vector2 scrollPostPatch = Vector2.zero;
        private Vector2 scrollPostInheritance = Vector2.zero;
        private int selectedPrePatchIndex = 0;
        private int selectedPostPatchIndex = 0;
        private int selectedPostInheritance = 0;
        private string indexSelectorBuffer = "";
        private bool errorXpath = false;

        // Caches
        private readonly float heuristicRatio = 0.5f;
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
            selectedList == SelectedList.prePatch ? XmlDocumentViewer_Mod.prePatchDocument :
            selectedList == SelectedList.postPatch ? XmlDocumentViewer_Mod.postPatchDocument : XmlDocumentViewer_Mod.postInheritanceDocument;

        public Dialog_XmlDocumentViewer()
        {
            doCloseX = true;
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new() { verticalSpacing = 0f };
            listing.Begin(inRect);

            // Gap for X button
            listing.Gap(10f);

            // Draw XPath Search
            Rect xpathEntryRect = listing.GetRect(buttonHeight);
            DrawXPathSearch(xpathEntryRect);

            listing.Gap(gapSize);

            // Draw XmlDocument buttons
            Rect buttonsRect = listing.GetRect(buttonHeight);
            Rect xmlDocButtonsRect = buttonsRect.LeftPart(codeViewportRatio);
            Rect nodeSelectionRect = buttonsRect.RightPartPixels(buttonsRect.width - xmlDocButtonsRect.width);
            DrawXmlDocumentButtons(xmlDocButtonsRect.LeftPartPixels(xmlDocButtonsRect.width - 2f));
            DrawNodeSelection(nodeSelectionRect.RightPartPixels(nodeSelectionRect.width - 2f));

            listing.GapLine(gapSize * 2);

            // Create Rects for viewport and sidemenu
            Rect viewportAndUIRect = listing.GetRect(inRect.height - listing.CurHeight - 4f);

            // Draw viewport
            Rect viewportRect = viewportAndUIRect.LeftPart(codeViewportRatio);
            DrawCodeViewport(viewportRect.LeftPartPixels(viewportRect.width - 2f));
            

            listing.End();
        }

        public override void OnAcceptKeyPressed()
        {
            if (GUI.GetNameOfFocusedControl() == "xpathTextField") { DoXPathSearch(); }
            base.OnAcceptKeyPressed();
        }

        public override void PostClose()
        {
            base.PostClose();
            lines.Clear();
            scrollPrePatch = scrollPostPatch = scrollPostInheritance = Vector2.zero;
            selectedPrePatchIndex = selectedPostPatchIndex = selectedPostInheritance = 0;
        }

        // Helper methods

        private void DoXPathSearch()
        {
            prePatchList = XmlDocumentViewer_Mod.prePatchDocument.SelectNodes(xpath);
            postPatchList = XmlDocumentViewer_Mod.postPatchDocument.SelectNodes(xpath);
            postInheritanceList = XmlDocumentViewer_Mod.postInheritanceDocument.SelectNodes(xpath);
            scrollPrePatch = scrollPostPatch = scrollPostInheritance = Vector2.zero;
            selectedPrePatchIndex = selectedPostPatchIndex = selectedPostInheritance = 0;

            if (CurrentResults != null && CurrentResults[0] != null)
            {
                XmlNode node = CurrentResults[0];
                ComputeLineMetrics(node);
            }
        }

        private void DrawXPathSearch(Rect inRect)
        {
            Rect xpathSearchButtonRect = inRect.RightPartPixels(128f);
            Rect xpathTextFieldRect = inRect.LeftPartPixels(inRect.width - xpathSearchButtonRect.width - 4f);
            GUI.SetNextControlName("xpathTextField");
            xpath = Widgets.TextField(xpathTextFieldRect, xpath);
            if (xpath.NullOrEmpty())
            {
                GUI.color = xpathTipColor;
                Rect xpathTipRect = new(xpathTextFieldRect.x, xpathTextFieldRect.y, xpathTextFieldRect.width, xpathTextFieldRect.height);
                xpathTipRect.y += 2f; xpathTipRect.x += 6f;
                Widgets.Label(xpathTipRect, "Enter XPath and select XmlDocument state:");
                GUI.color = Color.white;
            }
            GUI.color = XmlDocumentViewer_Mod.xmlViewerButtonColor;
            try
            {
                if (Widgets.ButtonText(xpathSearchButtonRect, "Search XPath"))
                {
                    DoXPathSearch();
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
            GUI.color = Color.white;
        }

        private void DrawXmlDocumentButtons(Rect inRect)
        {
            Rect buttonsRect = inRect;
            float buttonWidth = buttonsRect.width / 3f;
            Rect button1Rect = new(buttonsRect.x + 0 * buttonWidth, buttonsRect.y, buttonWidth - 2f, buttonHeight);
            Rect button2Rect = new(buttonsRect.x + 1 * buttonWidth + 2f, buttonsRect.y, buttonWidth - 4f, buttonHeight);
            Rect button3Rect = new(buttonsRect.x + 2 * buttonWidth + 2f, buttonsRect.y, buttonWidth - 2f, buttonHeight);
            
            if (selectedList == SelectedList.prePatch) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(button1Rect, $"Before Patching ({RichText.PrepareDataSizeLabel(XmlDocumentViewer_Mod.prePatchSize)} total)"))
            {
                selectedList = SelectedList.prePatch;
                if (CurrentResults != null && CurrentResults[0] != null)
                {
                    XmlNode node = CurrentResults[0];
                    ComputeLineMetrics(node);
                }
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(button1Rect, "View the XmlDocument before any patch operations have been run.");

            if (selectedList == SelectedList.postPatch) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(button2Rect, $"After Patching ({RichText.PrepareDataSizeLabel(XmlDocumentViewer_Mod.postPatchSize)} total)"))
            {
                selectedList = SelectedList.postPatch;
                if (CurrentResults != null && CurrentResults[0] != null)
                {
                    XmlNode node = CurrentResults[0];
                    ComputeLineMetrics(node);
                }
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(button2Rect, "View the XmlDocument after all patch operations but before inheritance.");

            if (selectedList == SelectedList.postInheritance) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(button3Rect, $"After Inheritance ({RichText.PrepareDataSizeLabel(XmlDocumentViewer_Mod.postInheritanceSize)} total)"))
            {
                selectedList = SelectedList.postInheritance;
                if (CurrentResults != null && CurrentResults[0] != null)
                {
                    XmlNode node = CurrentResults[0];
                    ComputeLineMetrics(node);
                }
            }

            GUI.color = Color.white;
            TooltipHandler.TipRegion(button3Rect, "View the XmlDocument after inheritance.");
        }

        private void DrawNodeSelection(Rect inRect)
        {
            float buttonWidth = 24f;
            Rect nextButtonRect = inRect.RightPartPixels(buttonWidth);
            Rect prevButtonRect = inRect.LeftPartPixels(buttonWidth);
            Rect textFieldRect = inRect.ContractedBy(buttonWidth, 0f);
            Widgets.TextFieldNumeric(textFieldRect, ref CurrentIndexRef(), ref indexSelectorBuffer, 0, 100);
            Widgets.ButtonText(nextButtonRect, ">");
            Widgets.ButtonText(prevButtonRect, "<");
        }

        private void DrawCodeViewport(Rect inRect)
        {
            XmlDocument doc = CurrentDocument;
            XmlNodeList results = CurrentResults;

            Widgets.DrawMenuSection(inRect);
            Rect outRect = inRect.ContractedBy(4f);
            
            // Error checking
            if (errorXpath) { Widgets.Label(outRect, "Invalid XPath."); return;  }
            else if (doc == null) { Widgets.Label(outRect, "Selected document not available."); return; }
            else if (results == null) { Widgets.Label(outRect, "Enter XPath and click \"Search XPath\" or press Enter."); return; }
            else if (results.Count == 0) { Widgets.Label(outRect, "No nodes found."); return; }
            else if (lines.Count == 0) { Widgets.Label(outRect, "Error."); return; }

            // Viewport split
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
                lineNumberStringBuilder.Append(i + 1);
                if (i < endLineIndex) lineNumberStringBuilder.Append('\n');
                codeStringBuilder.Append(lines[i]);
                if (i < endLineIndex) codeStringBuilder.Append('\n');
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
            Rect copyRect = inRect.RightPartPixels(32f).BottomPartPixels(32f);
            float horButtonPadding = 12f + (contentHeight >= outRect.height ? GenUI.ScrollBarWidth : 0f);
            float vertButtonPadding = 12f + (contentWidth >= outRect.width ? GenUI.ScrollBarWidth : 0f);

            copyRect.position -= new Vector2(horButtonPadding, vertButtonPadding);
            GUI.DrawTexture(copyRect, TexButton.Copy);
            Widgets.DrawHighlightIfMouseover(copyRect);
            TooltipHandler.TipRegion(copyRect, "Copy to clipboard");
            if (Widgets.ButtonInvisible(copyRect))
            {
                string plain = RichText.StripColorTags(outerXml);
                plain = RichText.PrepareIndentForCopy(plain);
                GUIUtility.systemCopyBuffer = plain;
                Messages.Message("Copied node to clipboard.", MessageTypeDefOf.TaskCompletion, historical: false);
            }
        }

        private void ComputeLineMetrics(XmlNode node)
        {
            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor prevAnchor = Text.Anchor;
            Text.Font = codeFont; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;

            lines.Clear();
            contentWidth = contentHeight = 0f;
            outerXml = RichText.ColorizeXml(node);
            string[] split = outerXml.Split('\n');
            int len = split.Length;
            while (split[len - 1].Length == 0 || split.GetLast().Length == 1) len--;
            //lineHeight = Text.CurFontStyle.codeFont.lineHeight;
            lineHeight = Text.CurFontStyle.lineHeight;
            for (int i = 0; i < len; i++)
            {
                string line = split[i];
                lines.Add(line);
                if (line.Length * (lineHeight * heuristicRatio) > contentWidth)
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
        private ref int CurrentIndexRef()
        {
            if (selectedList == SelectedList.prePatch) return ref selectedPrePatchIndex;
            if (selectedList == SelectedList.postPatch) return ref selectedPostPatchIndex;
            return ref selectedPostInheritance;
        }
    }
}
