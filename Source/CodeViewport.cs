using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    public class CodeViewport
    {
        // Viewport visuals
        public static readonly Color viewportColor = new(0.65f, 0.65f, 0.65f);
        public readonly GameFont codeFont = GameFont.Small;
        private float floatingMenuSize = 32f;
        private float floatingMenuPadding = 12f;
        private readonly float codeVerticalPadding = 2f;

        // Gutter visuals
        private static readonly Color LineNumberColor = new(0.4f, 0.4f, 0.4f, 1f);
        private const float gutterSeparatorThickness = 1f;
        private const float lineNumberRightPadding = 8f;
        private const float lineNumberLeftPad = 4f;
        private const float codeLeftPad = 4f;

        // Search visuals
        private readonly Color matchColor = new(255 / 255f, 215 / 255f, 0 / 255f);//new(255 / 255f, 0 / 255f, 255 / 255f); // purple
        private float fillRatio = 0.35f;
        private float borderRatio = 0.7f;
        private readonly int searchBorderThickness = 2;
        private readonly float markerHeight = 2f;
        private float jumpPadding = 48f;

        // Buffers
        internal int? pendingJumpLine = null;
        internal float? pendingJumpX = null;

        // Caches
        public float lineHeight = 0f;
        public float contentHeight = 0f;
        public float contentWidth = 0f;
        public float maxDigitWidth = 0f;
        public float spaceWidth = 0f;
        private readonly float heuristicRatio = 0.5f;
        List<string> cachedLines = [];
        private string formattedOuterXml = null;
        private GUIContent tmpTextGUIContent = new();

        public CodeViewport() 
        {
            
        }

        public void DrawCodeViewport(Rect inRect, ref TabData tabData, bool errorXpath = false, XmlDocument doc = null, bool doErrorCheck = true)
        {
            XmlNodeList results = tabData.resultNodeList;

            CustomWidgets.DrawColoredSection(inRect, viewportColor);

            Rect outRect = inRect.ContractedBy(4f);

            // Error checking
            if (doErrorCheck)
            {
                if (errorXpath) { Widgets.Label(outRect, "Invalid XPath."); return; }
                else if (doc == null) { Widgets.Label(outRect, "Selected document not available."); return; }
                else if (results == null) { Widgets.Label(outRect, "Enter XPath and click \"Search XPath\" or press Enter."); return; }
                else if (results.Count == 0) { Widgets.Label(outRect, "No nodes found."); return; }
                else if (cachedLines.Count == 0) { Widgets.Label(outRect, "Error."); return; }
            }

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

            ref Vector2 scroll = ref tabData.scrollPos;
            Widgets.BeginScrollView(codeViewRect, ref scroll, viewRect);

            // Visible slice
            float topY = scroll.y;
            float botY = topY + codeViewRect.height - GenUI.ScrollBarWidth;
            int startLineIndex = Mathf.Clamp((int)Mathf.Floor(topY / lineHeight), 0, totalLines - 1);
            int endLineIndex = Mathf.Clamp((int)Mathf.Ceil(botY / lineHeight) + 1, startLineIndex, totalLines - 1);

            // Build strings
            BuildStrings(out string textSlice, out string numsSlice);

            // Draw code inside the scrollview
            float yStart = startLineIndex * lineHeight;
            float blockH = (endLineIndex - startLineIndex + 1) * lineHeight;

            // Draw highlights
            DrawSearchHighlights(ref tabData);

            // Jump scrollbars if
            DoJump(ref scroll);

            // Draw XML
            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor prevAnchor = Text.Anchor; Color prevColor = GUI.color;
            Text.Font = codeFont; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Widgets.Label(new Rect(codeLeftPad, yStart, contentWidth + GenUI.ScrollBarWidth + codeVerticalPadding, blockH + GenUI.ScrollBarWidth + codeVerticalPadding), textSlice);
            Widgets.EndScrollView();

            DrawSearchMarkers(ref tabData);

            // Draw gutter outside the ScrollView
            Rect gutterRect = new(outRect.x, outRect.y, gutterSize, codeViewRect.height);
            Widgets.DrawBoxSolid(gutterRect, Widgets.MenuSectionBGFillColor * viewportColor);

            // Draw gutter separator
            Widgets.DrawBoxSolid(new Rect(gutterRect.xMax - gutterSeparatorThickness, outRect.y, gutterSeparatorThickness, codeViewRect.height), 0.7f * CustomWidgets.menuSectionBorderColor * viewportColor);

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
            Widgets.DrawBoxSolid(new Rect(outRect.x, gutterRect.yMax + gutterSeparatorThickness, outRect.width, gutterSeparatorThickness), 0.7f * CustomWidgets.menuSectionBorderColor * viewportColor);

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

            void BuildStrings(out string textSlice, out string numsSlice)
            {
                StringBuilder lineNumberStringBuilder = new((endLineIndex - startLineIndex + 1) * (lineNumberDigits + 1));
                StringBuilder codeStringBuilder = new((endLineIndex - startLineIndex + 1) * 64);
                for (int i = startLineIndex; i <= endLineIndex; i++)
                {
                    lineNumberStringBuilder.Append(i + 1);
                    if (i < endLineIndex) lineNumberStringBuilder.Append('\n');
                    codeStringBuilder.Append(cachedLines[i]);
                    if (i < endLineIndex) codeStringBuilder.Append('\n');
                }
                numsSlice = lineNumberStringBuilder.ToString();
                textSlice = codeStringBuilder.ToString();
            }

            void DrawSearchHighlights(ref TabData tabData)
            {
                List<MatchSpan> matches = tabData.matches;
                if (matches.Count > 0)
                {
                    GameFont prevTextFont = Text.Font; bool prevWordWrap = Text.WordWrap;
                    Text.Font = codeFont; Text.WordWrap = false;

                    for (int i = 0; i < matches.Count; i++)
                    {
                        MatchSpan match = matches[i];
                        if (match.line < startLineIndex || match.line > endLineIndex) continue;

                        float matchY = match.line * lineHeight + codeVerticalPadding;

                        Rect highlightBox = new(match.x, matchY, match.w, lineHeight);
                        Color matchFill = matchColor;
                        matchFill.a *= fillRatio;
                        Widgets.DrawBoxSolid(highlightBox, matchFill);

                        if (i == tabData.activeMatch)
                        {
                            Color matchActiveBorder = matchColor;
                            matchActiveBorder.a *= borderRatio;
                            GUI.color = matchActiveBorder;
                            Widgets.DrawBox(highlightBox, searchBorderThickness);
                            GUI.color = Color.white;
                        }
                    }
                    Text.WordWrap = prevWordWrap; Text.Font = prevTextFont;
                }
            }

            void DoJump(ref Vector2 scrollRef)
            {
                // Vertical jump
                if (pendingJumpLine.HasValue && visibleH > 0f)
                {
                    // target line band
                    float lineTop = pendingJumpLine.Value * lineHeight;
                    float lineBottom = lineTop + lineHeight;

                    // current visible band
                    float viewTop = scrollRef.y;
                    float viewBottom = viewTop + visibleH;

                    // padding margin (keep the target off the very edge)
                    float pad = jumpPadding;

                    bool fullyVisible = (lineTop >= viewTop + pad) && (lineBottom <= viewBottom - pad);

                    if (!fullyVisible)
                    {
                        if (lineTop < viewTop + pad)
                        {
                            scrollRef.y = Mathf.Max(0f, lineTop - pad);
                        }
                        else // lineBottom > viewBottom - pad
                        {
                            scrollRef.y = Mathf.Min(Mathf.Max(0f, contentHeight - visibleH), lineBottom + pad - visibleH);
                        }
                    }

                    pendingJumpLine = null;
                }

                // Horizontal jump
                if (pendingJumpX.HasValue && visibleW > 0f)
                {
                    float targetX = pendingJumpX.Value;
                    float pad = jumpPadding;

                    float viewLeft = scrollRef.x;
                    float viewRight = viewLeft + visibleW;

                    bool inside =
                        (targetX >= viewLeft + pad) &&
                        (targetX <= viewRight - pad);

                    if (!inside)
                    {
                        if (targetX < viewLeft + pad)
                            scrollRef.x = Mathf.Max(0f, targetX - pad);
                        else // targetX > viewRight - pad
                            scrollRef.x = Mathf.Min(Mathf.Max(0f, contentWidth - visibleW), targetX - visibleW * 0.5f);
                    }

                    pendingJumpX = null;
                }
            }

            void DrawSearchMarkers(ref TabData tabData)
            {
                float markerLength = GenUI.ScrollBarWidth * 3f / 4f;
                List<MatchSpan> currMatches = tabData.matches;

                if (currMatches != null && currMatches.Count != 0 && contentHeight > 0f && hasVScroll)
                {

                    float barW = markerLength;
                    Rect barRect = codeViewRect.RightPartPixels(GenUI.ScrollBarWidth).MiddlePartPixels(markerLength, codeViewRect.height).TrimBottomPartPixels(hasHScroll ? GenUI.ScrollBarWidth : 0f);
                    float inset = 1f;

                    // Bucketization to avoid overlap
                    float bucketH = Mathf.Max(markerHeight, barRect.height * lineHeight / contentHeight); // at least 2px tall
                    float drawableH = Mathf.Max(0f, barRect.height - 2f * inset);
                    int bucketCount = Mathf.Max(1, Mathf.FloorToInt(drawableH / bucketH));

                    // Count how many matches fall into each bucket, and track which bucket holds the active match
                    int activeIdx = tabData.activeMatch;
                    int activeBucket = -1;
                    int[] counts = new int[bucketCount];

                    for (int i = 0; i < currMatches.Count; i++)
                    {
                        MatchSpan matchSpan = currMatches[i];
                        float matchedMarkerY = Mathf.Lerp(barRect.y + inset, barRect.yMax - inset - bucketH, Mathf.Clamp01(matchSpan.normY));
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

            void DrawGutter()
            {

            }
        }

        public void QueueJumpTo(MatchSpan matchSpan)
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

        public void ReindexSearch(string searchText, ref TabData tabData)
        {
            ResetSearch(ref tabData);
            List<MatchSpan> listMatches = tabData.matches;

            tabData.needsIndexing = false;
            if (string.IsNullOrEmpty(searchText))
            {
                tabData.hasSearched = false;
                return;
            }
            tabData.hasSearched = true;

            if (cachedLines.Count == 0) return;


            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap;
            Text.Font = codeFont; Text.WordWrap = false;



            for (int line = 0; line < cachedLines.Count; line++)
            {
                string visibleText = RichText.StripColorTags(cachedLines[line]);
                int pos = 0;
                while (true)
                {
                    int idx = visibleText.IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) break;

                    // prefix width → x
                    tmpTextGUIContent.text = visibleText.Substring(0, idx);
                    float xLeft = codeLeftPad + Text.CurFontStyle.CalcSize(tmpTextGUIContent).x;

                    // match width → w
                    tmpTextGUIContent.text = visibleText.Substring(idx, searchText.Length);
                    float w = Text.CurFontStyle.CalcSize(tmpTextGUIContent).x;

                    // normalized vertical center for scrollbar markers
                    float norm = ((line + 0.5f) * lineHeight) / Mathf.Max(1f, contentHeight);

                    listMatches.Add(new MatchSpan
                    {
                        line = line,
                        start = idx,
                        length = searchText.Length,
                        x = xLeft,
                        w = w,
                        normY = norm
                    });

                    pos = idx + searchText.Length;
                }
            }
            Text.WordWrap = prevWrap; Text.Font = prevFont;
            if (listMatches.Count > 0) tabData.activeMatch = -1;
        }

        public void ResetSearch(ref TabData tabData)
        {
            tabData.matches.Clear();
            tabData.hasSearched = false;
            tabData.needsIndexing = true;
            tabData.activeMatch = -1;
        }

        private void ComputeGutterMetrics()
        {
            if (maxDigitWidth > 0f) { return; }

            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap;
            Text.Font = codeFont; Text.WordWrap = false;

            maxDigitWidth = 0f;
            for (char c = '0'; c <= '9'; c++)
                maxDigitWidth = Mathf.Max(maxDigitWidth, Text.CalcSize(c.ToString()).x);
            spaceWidth = Text.CalcSize(" ").x;

            Text.WordWrap = prevWrap; Text.Font = prevFont;
        }

        public void SetNodesToDraw(List<XmlNode> nodes, TabData tabData)
        {
            GameFont prevFont = Text.Font; bool prevWrap = Text.WordWrap; TextAnchor prevAnchor = Text.Anchor;
            Text.Font = codeFont; Text.WordWrap = false; Text.Anchor = TextAnchor.UpperLeft;

            cachedLines.Clear();
            contentWidth = contentHeight = 0f;
            formattedOuterXml = RichText.PrepareXml(nodes);
            if (tabData.selectedIndex > 0)
                formattedOuterXml = RichText.PrependIndexComment(formattedOuterXml, tabData.selectedIndex, tabData.resultNodeList.Count);
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
    }
}
