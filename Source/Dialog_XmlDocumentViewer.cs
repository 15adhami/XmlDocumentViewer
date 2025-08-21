using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using UnityEngine;
using Verse;
using static UnityEngine.GraphicsBuffer;
using static XmlDocumentViewer.TabData;

namespace XmlDocumentViewer
{
    internal class Dialog_XmlDocumentViewer : Window
    {
        // Menu visuals
        public override Vector2 InitialSize => new(Mathf.Min((float)UI.screenWidth * 0.9f, 1200f), Mathf.Min((float)UI.screenHeight * 0.9f, 900f));
        private readonly GameFont codeFont = GameFont.Small;
        private readonly float buttonGapSize = 2f;
        private readonly float buttonHeight = 24f;
        private readonly Color xpathTipColor = new(1f, 1f, 1f, 0.5f);
        private readonly float codeVerticalPadding = 2f;
        private readonly float codeViewportRatio = 0.70f;
        internal static readonly Color viewportColor = new(0.65f, 0.65f, 0.65f);
        private readonly Color xmlViewerButtonColor = new(120 / 255f, 255 / 255f, 120 / 255f);
        private float floatingMenuSize = 32f;
        private float floatingMenuPadding = 12f;

        // Gutter visuals
        private static readonly Color LineNumberColor = new(0.4f, 0.4f, 0.4f, 1f);
        private const float gutterSeparatorThickness = 1f;
        private const float lineNumberRightPadding = 8f;
        private const float lineNumberLeftPad = 4f;
        private const float codeLeftPad = 4f;

        // Search visuals
        private readonly Color matchColor = new(1f, 0.8f, 0.2f);//new(1f, 1f, 0f);
        private float fillRatio = 0.3f;
        private float borderRatio = 0.6f;
        private readonly int searchBorderThickness = 2;
        private readonly float markerHeight = 2f;

        // Private menu fields
        private string xpath = "";
        private TabData prePatchTabData = new(), postPatchTabData = new(), postInheritanceTabData = new();
        private SelectedList selectedList = SelectedList.prePatch;
        private string indexSelectorBuffer = "0";
        private bool errorXpath = false;
        private Stopwatch stopwatch = new();
        private GUIContent tmpTextGUIContent = new();

        // Search fields
        private string searchText = "";
        private int? pendingJumpLine = null;
        private float? pendingJumpX = null;

        // Caches
        private readonly float heuristicRatio = 0.5f;
        private readonly List<string> cachedLines = [];
        private string formattedOuterXml = null;
        private float contentWidth = 0f, contentHeight = 0f, lineHeight = 0f;
        private Vector2 codeViewportSize = Vector2.zero;

        // Gutter caches
        private float prevUiScale = -1f;
        private float maxDigitWidth = 0f, spaceWidth = 0f;

        private readonly Color menuSectionBorderColor = new ColorInt(135, 135, 135).ToColor;

        private enum SelectedList
        {
            prePatch,
            postPatch,
            postInheritance
        }

        // Getters/Setters

        private ref TabData CurrentTab()
        {
            switch (selectedList)
            {
                case SelectedList.prePatch: return ref prePatchTabData;
                case SelectedList.postPatch: return ref postPatchTabData;
                default: return ref postInheritanceTabData;
            }
        }

        private XmlDocument CurrentDocument =>
            selectedList == SelectedList.prePatch ? XmlDocumentViewer_Mod.prePatchDocument :
            selectedList == SelectedList.postPatch ? XmlDocumentViewer_Mod.postPatchDocument : XmlDocumentViewer_Mod.postInheritanceDocument;

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

            listing.Gap(buttonGapSize);

            // Draw XmlDocument buttons
            Rect buttonsRect = listing.GetRect(buttonHeight);
            Rect xmlDocButtonsRect = buttonsRect.LeftPart(codeViewportRatio);
            Rect nodeSelectionRect = buttonsRect.RightPartPixels(buttonsRect.width - xmlDocButtonsRect.width);
            DrawXmlDocumentButtons(xmlDocButtonsRect.TrimRightPartPixels(2f));
            DrawNodeSelection(nodeSelectionRect.TrimLeftPartPixels(2f));

            listing.GapLine(buttonGapSize * 2);

            // Create Rects for viewport and sidemenu
            Rect viewportAndSideMenuRect = listing.GetRect(inRect.height - listing.CurHeight - 4f);

            // Draw viewport
            Rect viewportRect = viewportAndSideMenuRect.LeftPart(codeViewportRatio);
            DrawCodeViewport(viewportRect.TrimRightPartPixels(2f));

            // Draw sidemenu
            Rect sidemenuRect = viewportAndSideMenuRect.RightPartPixels(viewportAndSideMenuRect.width - viewportRect.width);
            DrawSideMenu(sidemenuRect.TrimLeftPartPixels(2f));

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
            cachedLines.Clear();
            prePatchTabData.scrollPos = Vector2.zero;
            postPatchTabData.scrollPos = Vector2.zero;
            postInheritanceTabData.scrollPos = Vector2.zero;
            prePatchTabData.selectedIndex = 0;
            postPatchTabData.selectedIndex = 0;
            postInheritanceTabData.selectedIndex = 0;
        }

        // Drawing Methods

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
            Rect button1Rect = new(buttonsRect.x + 0 * buttonWidth, buttonsRect.y, buttonWidth - buttonGapSize / 2f, buttonHeight);
            Rect button2Rect = new(buttonsRect.x + 1 * buttonWidth + buttonGapSize / 2f, buttonsRect.y, buttonWidth - buttonGapSize, buttonHeight);
            Rect button3Rect = new(buttonsRect.x + 2 * buttonWidth + buttonGapSize / 2f, buttonsRect.y, buttonWidth - buttonGapSize / 2f, buttonHeight);

            // Gat data labels
            string prePatchDataLabel = null;
            string postPatchDataLabel = null;
            string postInheritanceDataLabel = null;

            if (CurrentTab().resultNodeList == null || CurrentTab().resultNodeList.Count == 0)
            {
                prePatchDataLabel = $"{RichText.PrepareDataSizeLabel(XmlDocumentViewer_Mod.prePatchSize)} total";
                postPatchDataLabel = $"{RichText.PrepareDataSizeLabel(XmlDocumentViewer_Mod.postPatchSize)} total";
                postInheritanceDataLabel = $"{RichText.PrepareDataSizeLabel(XmlDocumentViewer_Mod.postInheritanceSize)} total";
            }
            else if (CurrentTab().resultNodeList.Count > 0)
            {
                prePatchDataLabel = $"{RichText.PrepareDataSizeLabel(prePatchTabData.xpathSize)}";
                postPatchDataLabel = $"{RichText.PrepareDataSizeLabel(prePatchTabData.xpathSize)}";
                postInheritanceDataLabel = $"{RichText.PrepareDataSizeLabel(postInheritanceTabData.xpathSize)}";
            }

            // Draw buttons
            if (selectedList == SelectedList.prePatch) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(button1Rect, "Before Patching (" + prePatchDataLabel + ")"))
                DoButtonLogic(SelectedList.prePatch);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(button1Rect, "View the XmlDocument before any patch operations have been run.");

            if (selectedList == SelectedList.postPatch) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(button2Rect, "After Patching (" + postPatchDataLabel + ")"))
                DoButtonLogic(SelectedList.postPatch);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(button2Rect, "View the XmlDocument after all patch operations but before inheritance.");

            if (selectedList == SelectedList.postInheritance) { GUI.color = new Color(0.7f, 0.7f, 0.7f); }
            if (Widgets.ButtonText(button3Rect, "After Inheritance (" + postInheritanceDataLabel + ")"))
                DoButtonLogic(SelectedList.postInheritance);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(button3Rect, "View the XmlDocument after inheritance.");

            void DoButtonLogic(SelectedList selection)
            {
                bool searched = CurrentTab().hasSearched;
                selectedList = selection;
                UpdateCurrentResults();
                if (searched || string.IsNullOrEmpty(searchText)) { ReindexSearch(); }
            }
        }

        private void DrawNodeSelection(Rect inRect)
        {
            float buttonWidth = 28f;
            Rect nextButtonRect = inRect.RightPartPixels(buttonWidth);
            Rect prevButtonRect = inRect.LeftPartPixels(buttonWidth);
            Rect textFieldRect = inRect.ContractedBy(buttonWidth, 0f);

            int nodeCount = 0;
            if (CurrentTab().resultNodeList != null && CurrentTab().resultNodeList[0] != null) { nodeCount = CurrentTab().resultNodeList.Count; }
            int prevIndex = CurrentTab().selectedIndex;
            Widgets.TextFieldNumeric(textFieldRect, ref CurrentTab().selectedIndex, ref indexSelectorBuffer, 0, nodeCount);

            bool pressedButton = false;
            if (Widgets.ButtonText(nextButtonRect, ">"))
            {
                CurrentTab().selectedIndex++;
                pressedButton = true;
            }
            if (Widgets.ButtonText(prevButtonRect, "<"))
            {
                CurrentTab().selectedIndex--;
                pressedButton = true;
            }
            if (pressedButton)
            {
                CurrentTab().selectedIndex = Mathf.Clamp(CurrentTab().selectedIndex, 0, nodeCount);
                indexSelectorBuffer = CurrentTab().selectedIndex.ToString();
            }

            if (prevIndex != CurrentTab().selectedIndex)
            {
                UpdateCurrentResults();
                ReindexSearch();
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
            if (CurrentTab().selectedIndex == 0)
            {
                indexSelectorBuffer = "";
                Widgets.Label(adjustedTextFieldRect, "All");
            }

            GUI.color = Color.white;
        }

        private void DrawCodeViewport(Rect inRect)
        {
            XmlDocument doc = CurrentDocument;
            XmlNodeList results = CurrentTab().resultNodeList;

            CustomWidgets.DrawColoredSection(inRect, viewportColor);

            Rect outRect = inRect.ContractedBy(4f);

            // Error checking
            if (errorXpath) { Widgets.Label(outRect, "Invalid XPath."); return; }
            else if (doc == null) { Widgets.Label(outRect, "Selected document not available."); return; }
            else if (results == null) { Widgets.Label(outRect, "Enter XPath and click \"Search XPath\" or press Enter."); return; }
            else if (results.Count == 0) { Widgets.Label(outRect, "No nodes found."); return; }
            else if (cachedLines.Count == 0) { Widgets.Label(outRect, "Error."); return; }

            // Viewport split
            ComputeGutterMetrics();
            int totalLines = cachedLines.Count;
            int lineNumberDigits = Mathf.Max(2, totalLines > 0 ? (int)Mathf.Floor(Mathf.Log10(totalLines)) + 1 : 1);
            float gutterSize = lineNumberDigits * maxDigitWidth + spaceWidth + lineNumberLeftPad + lineNumberRightPadding;

            // Viewport
            float bottomBarHeight = 0f;
            float codeViewHeight = outRect.height - bottomBarHeight;
            Rect codeViewRect = new(outRect.x + gutterSize, outRect.y, outRect.width - gutterSize, codeViewHeight);
            bool hasVScroll = contentHeight > codeViewRect.height;
            bool hasHScroll = contentWidth > codeViewRect.width;
            float visibleH = codeViewRect.height - (hasHScroll ? GenUI.ScrollBarWidth : 0f);
            float visibleW = codeViewRect.width - (hasVScroll ? GenUI.ScrollBarWidth : 0f);

            float viewRectWidth = Mathf.Max(contentWidth + GenUI.ScrollBarWidth + codeVerticalPadding + codeLeftPad, codeViewRect.width - GenUI.ScrollBarWidth);
            float viewRectHeight = Mathf.Max(contentHeight + codeVerticalPadding, codeViewRect.height - GenUI.ScrollBarWidth);
            Rect viewRect = new(0f, 0f, viewRectWidth, viewRectHeight);

            ref Vector2 scroll = ref CurrentTab().scrollPos;
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
                codeStringBuilder.Append(cachedLines[i]);
                if (i < endLineIndex) codeStringBuilder.Append('\n');
            }
            string numsSlice = lineNumberStringBuilder.ToString();
            string textSlice = codeStringBuilder.ToString();

            // Draw code inside the scrollview
            float yStart = startLineIndex * lineHeight;
            float blockH = (endLineIndex - startLineIndex + 1) * lineHeight;



            // Draw highlights behind text (inside BeginScrollView)
            List<TabData.MatchSpan> matches = CurrentTab().matches;
            if (matches.Count > 0)
            {
                GameFont prevTextFont = Text.Font; bool prevWordWrap = Text.WordWrap;
                Text.Font = codeFont; Text.WordWrap = false;

                for (int i = 0; i < matches.Count; i++)
                {
                    TabData.MatchSpan match = matches[i];
                    if (match.line < startLineIndex || match.line > endLineIndex) continue;

                    string currLine = RichText.StripColorTags(cachedLines[match.line]);
                    tmpTextGUIContent.text = currLine.Substring(0, match.start);
                    float xLeft = codeLeftPad + Text.CurFontStyle.CalcSize(tmpTextGUIContent).x;
                    tmpTextGUIContent.text = currLine.Substring(match.start, match.length);
                    float width = Text.CurFontStyle.CalcSize(tmpTextGUIContent).x;
                    float y = match.line * lineHeight + codeVerticalPadding;

                    Rect highlightBox = new(xLeft, y, width, lineHeight);
                    Color matchFill = matchColor;
                    matchFill.a *= fillRatio;
                    Widgets.DrawBoxSolid(highlightBox, matchFill);

                    if (i == CurrentTab().activeMatch)
                    {
                        Color matchActiveBorder = matchColor;
                        matchActiveBorder.a *= 0.8f;
                        GUI.color = matchActiveBorder;
                        Widgets.DrawBox(highlightBox, searchBorderThickness);
                        GUI.color = Color.white;
                    }
                }
                Text.WordWrap = prevWordWrap; Text.Font = prevTextFont;
            }

            // Jump to search text
            DoScrollJumps();

            // Draw XML
            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor prevAnchor = Text.Anchor; Color prevColor = GUI.color;
            Text.Font = codeFont; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Widgets.Label(new Rect(codeLeftPad, yStart, contentWidth + GenUI.ScrollBarWidth + codeVerticalPadding, blockH + GenUI.ScrollBarWidth + codeVerticalPadding), textSlice);
            Widgets.EndScrollView();

            // Draw search markers
            DrawScrollbarSearchMarkers(codeViewRect);

            // Draw gutter outside the ScrollView
            Rect gutterRect = new(outRect.x, outRect.y, gutterSize, codeViewRect.height);
            Widgets.DrawBoxSolid(gutterRect, Widgets.MenuSectionBGFillColor * viewportColor);

            // Draw gutter separator
            Widgets.DrawBoxSolid(new Rect(gutterRect.xMax - gutterSeparatorThickness, outRect.y, gutterSeparatorThickness, codeViewRect.height), 0.7f * menuSectionBorderColor * viewportColor);

            // Draw line numbers outside the scrollview
            GUI.BeginGroup(outRect.TrimBottomPartPixels(bottomBarHeight));
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = LineNumberColor;
            float yStartFixed = yStart - scroll.y;
            float lineNumberRectWidth = gutterSize - gutterSeparatorThickness - lineNumberRightPadding - lineNumberLeftPad;//Mathf.Max(GenUI.ScrollBarWidth, lineHeight)
            GUI.Label(new Rect(lineNumberLeftPad, yStartFixed, lineNumberRectWidth, codeViewRect.height + Mathf.Max(GenUI.ScrollBarWidth, lineHeight)), numsSlice, Text.CurFontStyle);
            GUI.EndGroup();

            GUI.color = prevColor; Text.Anchor = prevAnchor; Text.WordWrap = prevWrap; Text.Font = prevFont;

            // Draw bottom bar
            Widgets.DrawBoxSolid(new Rect(outRect.x, gutterRect.yMax + gutterSeparatorThickness, outRect.width, gutterSeparatorThickness), 0.7f * menuSectionBorderColor * viewportColor);

            // Draw copy button
            Rect copyRect = inRect.RightPartPixels(floatingMenuSize).BottomPartPixels(floatingMenuSize);
            float horButtonPadding = floatingMenuPadding + (contentHeight >= outRect.height ? GenUI.ScrollBarWidth : 0f);
            float vertButtonPadding = floatingMenuPadding + (contentWidth >= outRect.width ? GenUI.ScrollBarWidth : 0f);

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

            void DoScrollJumps()
            {
                ref Vector2 scroll = ref CurrentTab().scrollPos;
                // Vertical jump
                if (pendingJumpLine.HasValue && visibleH > 0f)
                {
                    // target line band
                    float lineTop = pendingJumpLine.Value * lineHeight;
                    float lineBottom = lineTop + lineHeight;

                    // current visible band
                    float viewTop = scroll.y;
                    float viewBottom = viewTop + visibleH;

                    // padding margin (keep the target off the very edge)
                    float pad = Mathf.Max(4f, 0.5f * lineHeight);

                    bool fullyVisible = (lineTop >= viewTop + pad) && (lineBottom <= viewBottom - pad);

                    if (!fullyVisible)
                    {
                        if (lineTop < viewTop + pad)
                        {
                            scroll.y = Mathf.Max(0f, lineTop - pad);
                        }
                        else // lineBottom > viewBottom - pad
                        {
                            scroll.y = Mathf.Min(Mathf.Max(0f, contentHeight - visibleH), lineBottom + pad - visibleH);
                        }
                    }

                    pendingJumpLine = null;
                }

                // Horizontal jump
                if (pendingJumpX.HasValue && visibleW > 0f)
                {
                    float targetX = pendingJumpX.Value;
                    float pad = 10f;

                    float viewLeft = scroll.x;
                    float viewRight = viewLeft + visibleW;

                    bool inside =
                        (targetX >= viewLeft + pad) &&
                        (targetX <= viewRight - pad);

                    if (!inside)
                    {
                        if (targetX < viewLeft + pad)
                            scroll.x = Mathf.Max(0f, targetX - pad);
                        else // targetX > viewRight - pad
                            scroll.x = Mathf.Min(Mathf.Max(0f, contentWidth - visibleW), targetX - visibleW * 0.5f);
                    }

                    pendingJumpX = null;
                }
            }

            // TODO: Optimize highlights and markers
            // Draw markers over the vertical scrollbar (non-overlapping buckets)
            void DrawScrollbarSearchMarkers(Rect codeViewRect)
            {
                float markerLength = GenUI.ScrollBarWidth * 3f / 4f;
                List<MatchSpan> matches = CurrentTab().matches;

                if (matches == null || matches.Count == 0 || contentHeight <= 0f) return;
                if (!hasVScroll) return;
                
                float barW = markerLength;
                Rect barRect = codeViewRect.RightPartPixels(GenUI.ScrollBarWidth).MiddlePartPixels(markerLength, codeViewRect.height).TrimBottomPartPixels(hasHScroll ? GenUI.ScrollBarWidth : 0f);
                float inset = 1f;

                // Bucketization to avoid overlap
                float bucketH = Mathf.Max(2f, barRect.height * lineHeight / contentHeight);//Mathf.Max(2f, markerHeight); // at least 2px tall
                float drawableH = Mathf.Max(0f, barRect.height - 2f * inset);
                int bucketCount = Mathf.Max(1, Mathf.FloorToInt(drawableH / bucketH));

                // Count how many matches fall into each bucket, and track which bucket holds the active match
                int activeIdx = CurrentTab().activeMatch;
                int activeBucket = -1;
                int[] counts = new int[bucketCount];

                for (int i = 0; i < matches.Count; i++)
                {
                    MatchSpan matchSpan = matches[i];
                    float centerNorm = ((matchSpan.line + 0.5f) * lineHeight) / contentHeight;
                    // y position for this match's marker
                    float matchedMarkerY = Mathf.Lerp(barRect.y + inset, barRect.yMax - inset - bucketH, Mathf.Clamp01(centerNorm));
                    int bucketIndex = Mathf.Clamp((int)((matchedMarkerY - (barRect.y + inset)) / bucketH), 0, bucketCount - 1);
                    counts[bucketIndex]++;
                    if (i == activeIdx) activeBucket = bucketIndex;
                }

                // Draw all non-active buckets first (single rect per bucket)
                for (int b = 0; b < bucketCount; b++)
                {
                    int c = counts[b];
                    if (c == 0 || b == activeBucket) continue;

                    float markerY = barRect.y + inset + b * bucketH;
                    float h = Mathf.Min(bucketH, barRect.yMax - inset - markerY);

                    Color fill = matchColor;
                    float baseA = matchColor.a * fillRatio;
                    //fill.a = Mathf.Min(baseA, baseA * (0.35f + 0.65f * (1f - Mathf.Exp(-0.25f * c))));
                    fill.a = baseA;

                    Rect markerRect = new Rect(barRect.x + inset, markerY, barW - 2f * inset, h);
                    Widgets.DrawBoxSolid(markerRect, fill);
                }

                // Draw active bucket on top with stronger alpha + border to ensure visibility
                if (activeBucket >= 0)
                {
                    float markerY = barRect.y + inset + activeBucket * bucketH;
                    float h = Mathf.Min(bucketH, barRect.yMax - inset - markerY);

                    Rect activeMarkerRect = new Rect(barRect.x + inset, markerY, barW - 2f * inset, h);

                    Color activeFill = matchColor;
                    activeFill.a *= fillRatio;
                    Widgets.DrawBoxSolid(activeMarkerRect, activeFill);

                    Color border = matchColor; border.a *= borderRatio;
                    Color prev = GUI.color; GUI.color = border;
                    Widgets.DrawBox(activeMarkerRect, 1);
                    GUI.color = prev;
                }
            }


        }

        private void DrawSideMenu(Rect inRect)
        {
            // Draw background setion
            CustomWidgets.DrawColoredSection(inRect, viewportColor);

            // Begin listing
            Listing_Standard listing = new();
            Rect listingRect = inRect.ContractedBy(16f, 8f);
            listing.Begin(listingRect);

            // Draw search section
            Rect searchSectionRect = listing.GetRect(2 * Text.LineHeight + buttonHeight + buttonGapSize);
            DrawSearchBlock(searchSectionRect);

            listing.GapLine(6f);

            listing.End();

            // Inner methods

            void DrawSearchBlock(Rect inSectionRect)
            {
                Rect searchHeaderRect = inSectionRect.TopPartPixels(Text.LineHeight);
                Rect searchboxRect = inSectionRect.TopPartPixels(2 * Text.LineHeight).BottomPartPixels(Text.LineHeight);
                Rect findButtonsRect = inSectionRect.BottomPartPixels(inSectionRect.height - searchboxRect.height - searchHeaderRect.height);

                // Draw search header and count
                Widgets.Label(searchHeaderRect, "Search:");
                Text.Anchor = TextAnchor.UpperRight;
                if (CurrentTab().hasSearched && CurrentTab().activeMatch >= 0)
                    Widgets.Label(searchHeaderRect, (CurrentTab().activeMatch + 1).ToString() + " / " + CurrentTab().matches.Count.ToString());
                else if (CurrentTab().hasSearched && CurrentTab().activeMatch < 0)
                    Widgets.Label(searchHeaderRect, CurrentTab().matches.Count.ToString() + " Result(s)");
                Text.Anchor = TextAnchor.UpperLeft;

                string prevCurrentSearchText = searchText;
                searchText = Widgets.TextField(searchboxRect, searchText);
                if (prevCurrentSearchText != searchText) { CurrentTab().needsIndexing = true; }

                findButtonsRect.TrimTopPartPixels(buttonGapSize).SplitVerticallyWithMargin(out Rect prevButtonRect, out Rect nextButtonRect, buttonGapSize);
                if (Widgets.ButtonText(prevButtonRect, "Previous"))
                    DoButtonLogic(-1);
                if (Widgets.ButtonText(nextButtonRect, "Next"))
                    DoButtonLogic(1);

                void DoButtonLogic(int increment)
                {
                    bool reIndexed = false;
                    if (CurrentTab().needsIndexing)
                    {
                        ReindexSearch();
                        reIndexed = true;
                    }
                    List<TabData.MatchSpan> matchList = CurrentTab().matches;
                    if (matchList.Count > 0)
                    {
                        int idx = CurrentTab().activeMatch;
                        if (!reIndexed)
                        {
                            if (idx != -1)
                            {
                                // Manual modulus
                                idx = idx + increment;
                                if (idx >= matchList.Count)
                                    idx -= matchList.Count;
                                if (idx < 0)
                                    idx += matchList.Count;
                            }
                            else
                            {
                                if (increment > 0) { idx = 0; }
                                else { idx = matchList.Count - 1; }
                            }
                            QueueJumpTo(matchList[idx]);
                        }
                        else
                            idx = -1;
                        CurrentTab().activeMatch = idx;
                    }
                }
            }
        }

        // Helper methods

        private void DoXPathSearch()
        {
            try
            {
                prePatchTabData.ClearAll();
                postPatchTabData.ClearAll();
                postInheritanceTabData.ClearAll();
                
                DoXPathSearch(XmlDocumentViewer_Mod.prePatchDocument, ref prePatchTabData);
                DoXPathSearch(XmlDocumentViewer_Mod.postPatchDocument, ref postPatchTabData);
                DoXPathSearch(XmlDocumentViewer_Mod.postInheritanceDocument, ref postInheritanceTabData);

                errorXpath = false;
            }
            catch
            {
                errorXpath = true;
                prePatchTabData.resultNodeList = null;
                postPatchTabData.resultNodeList = null;
                postInheritanceTabData.resultNodeList = null;
            }
            pendingJumpLine = null;
            pendingJumpX = null;

            prePatchTabData.xpathSize = ComputeByteCount(prePatchTabData.resultNodeList);
            postPatchTabData.xpathSize = ComputeByteCount(postPatchTabData.resultNodeList);
            postInheritanceTabData.xpathSize = ComputeByteCount(postInheritanceTabData.resultNodeList);
            UpdateCurrentResults();

            if (CurrentTab().hasSearched || string.IsNullOrEmpty(searchText)) { ReindexSearch(); }

            void DoXPathSearch(XmlDocument xmlDoc, ref TabData tab)
            {
                stopwatch.Reset();
                stopwatch.Start();
                tab.resultNodeList = xmlDoc.SelectNodes(xpath);
                stopwatch.Stop();
                tab.timer = stopwatch.Elapsed.Milliseconds;
                stopwatch.Reset();
            }
        }

        private void SetNodesToDraw(List<XmlNode> nodes)
        {
            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor prevAnchor = Text.Anchor;
            Text.Font = codeFont; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;

            cachedLines.Clear();
            contentWidth = contentHeight = 0f;
            formattedOuterXml = RichText.PrepareXml(nodes);
            if (CurrentTab().selectedIndex > 0)
                formattedOuterXml = RichText.PrependIndexComment(formattedOuterXml, CurrentTab().selectedIndex, CurrentTab().resultNodeList.Count);
            string[] split = formattedOuterXml.Split('\n');
            int len = split.Length;
            while (split[len - 1].Length == 0 || split.GetLast().Length == 1) len--;
            //lineHeight = Text.CurFontStyle.codeFont.lineHeight;
            lineHeight = Text.CurFontStyle.lineHeight;
            for (int i = 0; i < len; i++)
            {
                string line = split[i];
                cachedLines.Add(line);
                if (line.Length * (lineHeight * heuristicRatio) > contentWidth)
                {
                    Vector2 size = Text.CalcSize(line);
                    if (size.x > contentWidth) contentWidth = size.x;
                }
            }
            contentHeight = cachedLines.Count * lineHeight;

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

        private void UpdateCurrentResults()
        {
            if (CurrentTab().resultNodeList == null || CurrentTab().resultNodeList.Count == 0) return;
            if (CurrentTab().selectedIndex > 0 && CurrentTab().resultNodeList[CurrentTab().selectedIndex - 1] != null)
                SetNodesToDraw([CurrentTab().resultNodeList[CurrentTab().selectedIndex - 1]]);
            else if (CurrentTab().selectedIndex == 0) SetNodesToDraw(CurrentTab().resultNodeList.ToList());
            indexSelectorBuffer = CurrentTab().selectedIndex.ToString();
        }

        private void ReindexSearch()
        {
            CurrentTab().needsIndexing = false;
            List<TabData.MatchSpan> listMatches = CurrentTab().matches;
            listMatches.Clear();
            CurrentTab().activeMatch = -1;

            if (string.IsNullOrEmpty(searchText))
            {
                CurrentTab().hasSearched = false;
                return;
            }
            else
                CurrentTab().hasSearched = true;

            if (cachedLines.Count == 0)
                return;

            for (int line = 0; line < cachedLines.Count; line++)
            {
                string visibleText = RichText.StripColorTags(cachedLines[line]);
                int pos = 0;
                while (true)
                {
                    int idx = visibleText.IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) break;
                    listMatches.Add(new TabData.MatchSpan { line = line, start = idx, length = searchText.Length });
                    pos = idx + searchText.Length;
                }
            }
            if (listMatches.Count > 0) CurrentTab().activeMatch = -1;
        }

        private void QueueJumpTo(TabData.MatchSpan matchSpan)
        {
            // Vertical target:
            pendingJumpLine = matchSpan.line;

            // Horizontal target: width of visible prefix up to match start
            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap;
            Text.Font = codeFont; Text.WordWrap = false;
            string visible = RichText.StripColorTags(cachedLines[matchSpan.line]);
            tmpTextGUIContent.text = visible.Substring(0, matchSpan.start);
            float xLeft = codeLeftPad + Text.CurFontStyle.CalcSize(tmpTextGUIContent).x;
            Text.WordWrap = prevWrap; Text.Font = prevFont;

            pendingJumpX = xLeft;
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
