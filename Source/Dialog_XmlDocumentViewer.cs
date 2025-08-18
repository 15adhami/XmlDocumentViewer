using RimWorld;
using System;
using System.Collections.Generic;
using System.Runtime;
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
        private readonly float codeViewportRatio = 0.70f;
        private readonly Color viewportColor = new(0.65f, 0.65f, 0.65f);
        private readonly Color xmlViewerButtonColor = new(120 / 255f, 255 / 255f, 120 / 255f);

        // Gutter visuals
        private static readonly Color LineNumberColor = new(0.4f, 0.4f, 0.4f, 1f);
        private const float gutterSeparatorThickness = 1f;
        private const float lineNumberRightPadding = 8f;
        private const float lineNumberLeftPad = 4f;
        private const float codeLeftPad = 4f;

        // Search visuals
        private readonly Color matchFill = new(1f, 1f, 0f, 0.20f);
        private readonly Color matchActiveFill = new(1f, 0.9f, 0.3f, 0.35f);
        private readonly Color matchActiveBorder = new(1f, 0.9f, 0.3f, 0.85f);

        // Private menu fields
        private string xpath = "";
        private XmlNodeList prePatchList, postPatchList, postInheritanceList;
        private SelectedList selectedList = SelectedList.prePatch;
        private Vector2 scrollPrePatch, scrollPostPatch, scrollPostInheritance = Vector2.zero;
        private int selectedPrePatchIndex, selectedPostPatchIndex, selectedPostInheritanceIndex = 0;
        private int selectedPrePatchSize, selectedPostPatchSize, selectedPostInheritanceSize = 0;
        private string searchTextPrePatch, searchTextPostPatch, searchTextPostInheritance = "";
        private string indexSelectorBuffer = "0";
        private bool errorXpath = false;

        // Search fields
        private struct MatchSpan { public int line; public int start; public int length; }
        private readonly List<MatchSpan> matchesPrePatch = new();
        private readonly List<MatchSpan> matchesPostPatch = new();
        private readonly List<MatchSpan> matchesPostInheritance = new();

        private int activeMatchPrePatch = -1, activeMatchPostPatch = -1, activeMatchPostInheritance = -1;
        private int? pendingJumpLine = null;
        private float? pendingJumpX = null;

        // Caches
        private readonly float heuristicRatio = 0.5f;
        private readonly List<string> lines = [];
        private string formattedOuterXml = null;
        private float contentWidth, contentHeight, lineHeight = 0f;

        // Gutter caches
        private float prevUiScale = -1f;
        private float maxDigitWidth, spaceWidth = 0f;

        private readonly Color menuSectionBorderColor = new ColorInt(135, 135, 135).ToColor;

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

        private string CurrentSearchText
        {
            get =>
                selectedList == SelectedList.prePatch ? searchTextPrePatch :
                selectedList == SelectedList.postPatch ? searchTextPostPatch : searchTextPostInheritance;
            set
            {
                if (selectedList == SelectedList.prePatch) searchTextPrePatch = value;
                else if (selectedList == SelectedList.postPatch) searchTextPostPatch = value;
                else if (selectedList == SelectedList.postInheritance) searchTextPostInheritance = value;
            }
        }

        private List<MatchSpan> CurrentMatches() =>
            selectedList == SelectedList.prePatch ? matchesPrePatch :
            selectedList == SelectedList.postPatch ? matchesPostPatch : matchesPostInheritance;

        private ref int CurrentActiveMatchRef()
        {
            if (selectedList == SelectedList.prePatch) return ref activeMatchPrePatch;
            if (selectedList == SelectedList.postPatch) return ref activeMatchPostPatch;
            return ref activeMatchPostInheritance;
        }

        // Constructor

        public Dialog_XmlDocumentViewer()
        {
            doCloseX = true;
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        // Overrides

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
            Rect viewportAndSideMenuRect = listing.GetRect(inRect.height - listing.CurHeight - 4f);

            // Draw viewport
            Rect viewportRect = viewportAndSideMenuRect.LeftPart(codeViewportRatio);
            DrawCodeViewport(viewportRect.LeftPartPixels(viewportRect.width - 2f));

            // Draw sidemenu
            Rect sidemenuRect = viewportAndSideMenuRect.RightPartPixels(viewportAndSideMenuRect.width - viewportRect.width);
            DrawSideMenu(sidemenuRect.RightPartPixels(sidemenuRect.width - 2f));

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
            selectedPrePatchIndex = selectedPostPatchIndex = selectedPostInheritanceIndex = 0;
        }

        // Helper methods

        private void DoXPathSearch()
        {
            try
            {
                prePatchList = XmlDocumentViewer_Mod.prePatchDocument.SelectNodes(xpath);
                postPatchList = XmlDocumentViewer_Mod.postPatchDocument.SelectNodes(xpath);
                postInheritanceList = XmlDocumentViewer_Mod.postInheritanceDocument.SelectNodes(xpath);
                errorXpath = false;
            }
            catch
            {
                errorXpath = true;
                prePatchList = null;
                postPatchList = null;
                postInheritanceList = null;
            }
            searchTextPrePatch = searchTextPostPatch = searchTextPostInheritance = "";
            scrollPrePatch = scrollPostPatch = scrollPostInheritance = Vector2.zero;
            selectedPrePatchIndex = selectedPostPatchIndex = selectedPostInheritanceIndex = 0;
            selectedPrePatchSize = ComputeByteCount(prePatchList);
            selectedPostPatchSize = ComputeByteCount(postPatchList);
            selectedPostInheritanceSize = ComputeByteCount(postInheritanceList);
            UpdateCurrentResults();
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
            GUI.color = xmlViewerButtonColor;
            if (Widgets.ButtonText(xpathSearchButtonRect, "")) { DoXPathSearch(); }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(xpathSearchButtonRect, "Search XPath");
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawXmlDocumentButtons(Rect inRect)
        {
            Rect buttonsRect = inRect;
            float buttonWidth = buttonsRect.width / 3f;
            Rect button1Rect = new(buttonsRect.x + 0 * buttonWidth, buttonsRect.y, buttonWidth - 2f, buttonHeight);
            Rect button2Rect = new(buttonsRect.x + 1 * buttonWidth + 2f, buttonsRect.y, buttonWidth - 4f, buttonHeight);
            Rect button3Rect = new(buttonsRect.x + 2 * buttonWidth + 2f, buttonsRect.y, buttonWidth - 2f, buttonHeight);

            // Gat data labels
            string prePatchDataLabel = null;
            string postPatchDataLabel = null;
            string postInheritanceDataLabel = null;

            if (CurrentResults == null || CurrentResults.Count == 0)
            {
                prePatchDataLabel = $"{RichText.PrepareDataSizeLabel(XmlDocumentViewer_Mod.prePatchSize)} total";
                postPatchDataLabel = $"{RichText.PrepareDataSizeLabel(XmlDocumentViewer_Mod.postPatchSize)} total";
                postInheritanceDataLabel = $"{RichText.PrepareDataSizeLabel(XmlDocumentViewer_Mod.postInheritanceSize)} total";
            }
            else if (CurrentResults.Count > 0)
            {
                prePatchDataLabel = $"{RichText.PrepareDataSizeLabel(selectedPrePatchSize)}";
                postPatchDataLabel = $"{RichText.PrepareDataSizeLabel(selectedPostPatchSize)}";
                postInheritanceDataLabel = $"{RichText.PrepareDataSizeLabel(selectedPostInheritanceSize)}";
            }

            // Draw buttons
            if (selectedList == SelectedList.prePatch) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(button1Rect, "Before Patching (" + prePatchDataLabel + ")"))
            {
                selectedList = SelectedList.prePatch;
                UpdateCurrentResults();
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(button1Rect, "View the XmlDocument before any patch operations have been run.");

            if (selectedList == SelectedList.postPatch) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(button2Rect, "After Patching (" + postPatchDataLabel + ")"))
            {
                selectedList = SelectedList.postPatch;
                UpdateCurrentResults();
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(button2Rect, "View the XmlDocument after all patch operations but before inheritance.");

            if (selectedList == SelectedList.postInheritance) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(button3Rect, "After Inheritance (" + postInheritanceDataLabel + ")"))
            {
                selectedList = SelectedList.postInheritance;
                UpdateCurrentResults();
            }

            GUI.color = Color.white;
            TooltipHandler.TipRegion(button3Rect, "View the XmlDocument after inheritance.");
        }

        private void DrawNodeSelection(Rect inRect)
        {
            float buttonWidth = 28f;
            Rect nextButtonRect = inRect.RightPartPixels(buttonWidth);
            Rect prevButtonRect = inRect.LeftPartPixels(buttonWidth);
            Rect textFieldRect = inRect.ContractedBy(buttonWidth, 0f);

            int nodeCount = 0;
            if (CurrentResults != null && CurrentResults[0] != null) { nodeCount = CurrentResults.Count; }
            int prevIndex = CurrentIndexRef();
            Widgets.TextFieldNumeric(textFieldRect, ref CurrentIndexRef(), ref indexSelectorBuffer, 0, nodeCount);

            bool pressedButton = false;
            if (Widgets.ButtonText(nextButtonRect, ">"))
            {
                CurrentIndexRef()++;
                pressedButton = true;
            }
            if (Widgets.ButtonText(prevButtonRect, "<"))
            {
                CurrentIndexRef()--;
                pressedButton = true;
            }
            if (pressedButton)
            {
                CurrentIndexRef() = Mathf.Clamp(CurrentIndexRef(), 0, nodeCount);
                indexSelectorBuffer = CurrentIndexRef().ToString();
            }

            if (prevIndex != CurrentIndexRef())
            {
                UpdateCurrentResults();
            }

            // Draw total count labels
            GUI.color = xpathTipColor;
            Rect adjustedTextFieldRect = new(textFieldRect.x, textFieldRect.y, textFieldRect.width, textFieldRect.height);
            adjustedTextFieldRect.y += 2f;
            adjustedTextFieldRect = adjustedTextFieldRect.ContractedBy(6f, 0f);
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(adjustedTextFieldRect, "/");
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(adjustedTextFieldRect, nodeCount.ToString() + " node(s)");
            Text.Anchor = TextAnchor.UpperLeft;
            if (CurrentIndexRef() == 0)
            {
                indexSelectorBuffer = "";
                Widgets.Label(adjustedTextFieldRect, "All");
            }
                
            GUI.color = Color.white;
        }

        private void DrawCodeViewport(Rect inRect)
        {
            XmlDocument doc = CurrentDocument;
            XmlNodeList results = CurrentResults;

            DrawViewportBackground(inRect);

            Rect outRect = inRect.ContractedBy(4f);

            // Error checking
            if (errorXpath) { Widgets.Label(outRect, "Invalid XPath."); return; }
            else if (doc == null) { Widgets.Label(outRect, "Selected document not available."); return; }
            else if (results == null) { Widgets.Label(outRect, "Enter XPath and click \"Search XPath\" or press Enter."); return; }
            else if (results.Count == 0) { Widgets.Label(outRect, "No nodes found."); return; }
            else if (lines.Count == 0) { Widgets.Label(outRect, "Error."); return; }

            // Viewport split
            ComputeGutterMetrics();
            int totalLines = lines.Count;
            int lineNumberDigits = Mathf.Max(2, totalLines > 0 ? (int)Mathf.Floor(Mathf.Log10(totalLines)) + 1 : 1);
            float gutterSize = lineNumberDigits * maxDigitWidth + spaceWidth + lineNumberLeftPad + lineNumberRightPadding;

            // Viewport
            float bottomBarHeight = 0f;
            float codePadding = 2f;
            float codeViewHeight = outRect.height - bottomBarHeight;
            Rect codeViewRect = new(outRect.x + gutterSize, outRect.y, outRect.width - gutterSize, codeViewHeight);
            float viewRectWidth = Mathf.Max(contentWidth + GenUI.ScrollBarWidth + codePadding + codeLeftPad, codeViewRect.width - GenUI.ScrollBarWidth);
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
            GUI.Label(new Rect(codeLeftPad, yStart, contentWidth + GenUI.ScrollBarWidth + codePadding, blockH + GenUI.ScrollBarWidth + codePadding), textSlice, Text.CurFontStyle);
            Widgets.EndScrollView();

            // Draw gutter outside the ScrollView
            Rect gutterRect = new(outRect.x, outRect.y, gutterSize, codeViewRect.height);
            Widgets.DrawBoxSolid(gutterRect, Widgets.MenuSectionBGFillColor * viewportColor);

            // Draw gutter separator
            Widgets.DrawBoxSolid(new Rect(gutterRect.xMax - gutterSeparatorThickness, outRect.y, gutterSeparatorThickness, codeViewRect.height), 0.7f * menuSectionBorderColor * viewportColor);

            // Draw line numbers outside the scrollview
            GUI.BeginGroup(outRect.TopPartPixels(outRect.height - bottomBarHeight));
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = LineNumberColor;
            float yStartFixed = yStart - scroll.y;
            float lineNumberRectWidth = gutterSize - gutterSeparatorThickness - lineNumberRightPadding - lineNumberLeftPad;//Mathf.Max(GenUI.ScrollBarWidth, lineHeight)
            GUI.Label(new Rect(lineNumberLeftPad, yStartFixed, lineNumberRectWidth, codeViewRect.height + Mathf.Max(GenUI.ScrollBarWidth, lineHeight)), numsSlice, Text.CurFontStyle);
            GUI.EndGroup();

            GUI.color = prevColor; Text.Anchor = pervAnchor; Text.WordWrap = prevWrap; Text.Font = prevFont;

            // Draw bottom bar
            Widgets.DrawBoxSolid(new Rect(outRect.x, gutterRect.yMax + gutterSeparatorThickness, outRect.width, gutterSeparatorThickness), 0.7f * menuSectionBorderColor * viewportColor);

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
                string plain = RichText.StripColorTags(formattedOuterXml);
                plain = RichText.PrepareIndentForCopy(plain);
                GUIUtility.systemCopyBuffer = plain;
                Messages.Message("Copied node to clipboard.", MessageTypeDefOf.TaskCompletion, historical: false);
            }

            void DrawViewportBackground(Rect inViewportRect)
            {
                GUI.color = Widgets.MenuSectionBGFillColor * viewportColor;
                GUI.DrawTexture(inViewportRect, BaseContent.WhiteTex);
                GUI.color = menuSectionBorderColor * viewportColor;
                Widgets.DrawBox(inViewportRect, 1, null);
                GUI.color = Color.white;
            }
        }

        private void DrawSideMenu(Rect inRect)
        {
            // Draw background setion
            GUI.color = Widgets.MenuSectionBGFillColor * viewportColor;
            GUI.DrawTexture(inRect, BaseContent.WhiteTex);
            GUI.color = new ColorInt(135, 135, 135).ToColor * viewportColor;
            Widgets.DrawBox(inRect, 1, null);
            GUI.color = Color.white;

            // Begin listing
            Listing_Standard listing = new();
            Rect listingRect = inRect.ContractedBy(16f, 8f);
            listing.Begin(listingRect);
            
            // Draw search section
            Rect searchSectionRect = listing.GetRect(2 * Text.LineHeight + buttonHeight);
            Rect searchHeaderRect = searchSectionRect.TopPartPixels(Text.LineHeight);
            Rect searchboxRect = searchSectionRect.TopPartPixels(2 * Text.LineHeight).BottomPartPixels(Text.LineHeight);
            Rect findButtonsRect = searchSectionRect.BottomPartPixels(buttonHeight);
            Widgets.Label(searchHeaderRect, "Search:");
            CurrentSearchText = Widgets.TextField(searchboxRect, CurrentSearchText);
            Widgets.ButtonText(findButtonsRect.LeftHalf(), "Previous");
            Widgets.ButtonText(findButtonsRect.RightHalf(), "Next");
            listing.GapLine();
            //Rect gapLineRect = listing.GetRect(12f);
            //Widgets.DrawBoxSolid(new Rect(gapLineRect.x + 12f, gapLineRect.center.y + 1f, gapLineRect.width - 2 * 12f, 1f), 0.7f * menuSectionBorderColor * viewportColor);

            listing.End();
            
        }

        private void SetNodesToDraw(List<XmlNode> nodes)
        {
            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor prevAnchor = Text.Anchor;
            Text.Font = codeFont; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;

            lines.Clear();
            contentWidth = contentHeight = 0f;
            formattedOuterXml = RichText.PrepareXml(nodes);
            if (CurrentIndexRef() > 0)
                formattedOuterXml = RichText.PrependIndexComment(formattedOuterXml, CurrentIndexRef(), CurrentResults.Count);
            string[] split = formattedOuterXml.Split('\n');
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
            return ref selectedPostInheritanceIndex;
        }

        private void UpdateCurrentResults()
        {
            if (CurrentResults == null || CurrentResults.Count == 0) return;
            if (CurrentIndexRef() > 0 && CurrentResults[CurrentIndexRef() - 1] != null)
                SetNodesToDraw([CurrentResults[CurrentIndexRef() - 1]]);
            else if (CurrentIndexRef() == 0) SetNodesToDraw(CurrentResults.ToList());
            indexSelectorBuffer = CurrentIndexRef().ToString();
        }

        private void ReindexSearchForCurrent()
        {
            List<MatchSpan> listMatches = CurrentMatches();
            listMatches.Clear();
            CurrentActiveMatchRef() = -1;

            string needle = CurrentSearchText;
            if (string.IsNullOrEmpty(needle) || lines.Count == 0) return;

            for (int line = 0; line < lines.Count; line++)
            {
                string visibleText = RichText.StripColorTags(lines[line]);
                int pos = 0;
                while (true)
                {
                    int idx = visibleText.IndexOf(needle, pos, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) break;
                    listMatches.Add(new MatchSpan { line = line, start = idx, length = needle.Length });
                    pos = idx + needle.Length;
                }
            }
            if (listMatches.Count > 0) CurrentActiveMatchRef() = 0;
        }


        private int ComputeByteCount(XmlNodeList nodes)
        {
            int count = 0;
            if (nodes == null) return count;
            foreach (XmlNode node in nodes)
                count += Encoding.UTF8.GetByteCount(node?.OuterXml);
            return count;
        }

    }
}
