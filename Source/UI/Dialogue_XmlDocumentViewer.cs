using RimWorld;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    internal class Dialogue_XmlDocumentViewer : Window
    {
        // Menu visuals
        public override Vector2 InitialSize => new(Mathf.Min((float)UI.screenWidth * 0.9f, 1200f), Mathf.Min((float)UI.screenHeight * 0.9f, 900f));
        //private readonly GameFont codeFont = GameFont.Small;
        private readonly float buttonGapSize = 2f;
        private readonly float buttonHeight = 24f;
        private readonly Color xpathTipColor = new(1f, 1f, 1f, 0.5f);
        private readonly float codeViewportRatio = 0.70f;
        internal static readonly Color viewportColor = new(0.65f, 0.65f, 0.65f);
        private readonly Color xmlViewerButtonColor = new(120 / 255f, 255 / 255f, 120 / 255f);

        // Private menu fields
        private string xpath = "";
        private TabData prePatchTabData = new(), postPatchTabData = new(), postInheritanceTabData = new();
        private CodeViewport codeViewport = new();
        private SelectedList selectedList = SelectedList.prePatch;
        private string indexSelectorBuffer = "0";
        private bool errorXpath = false;
        private Stopwatch stopwatch = new();
        private bool isFirstFrame = true;

        // Search fields
        private string searchText = "";

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
            selectedList == SelectedList.prePatch ? XmlDocumentManager.prePatchDocument :
            selectedList == SelectedList.postPatch ? XmlDocumentManager.postPatchDocument : XmlDocumentManager.postInheritanceDocument;

        // Constructor

        public Dialogue_XmlDocumentViewer()
        {
            doCloseX = true;
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
            prePatchTabData.xmlDocument = XmlDocumentManager.prePatchDocument;
            postPatchTabData.xmlDocument = XmlDocumentManager.postPatchDocument;
            postInheritanceTabData.xmlDocument = XmlDocumentManager.postInheritanceDocument;
            prePatchTabData.documentType = TabData.DocumentType.PrePatch;
            postPatchTabData.documentType = TabData.DocumentType.PostPatch;
            postInheritanceTabData.documentType = TabData.DocumentType.PostInheritance;
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
            codeViewport.DrawCodeViewport(viewportRect.TrimRightPartPixels(2f), ref CurrentTab(), errorXpath, CurrentDocument);

            // Draw sidemenu
            Rect sidemenuRect = viewportAndSideMenuRect.RightPartPixels(viewportAndSideMenuRect.width - viewportRect.width);
            DrawSideMenu(sidemenuRect.TrimLeftPartPixels(2f));

            listing.End();
            isFirstFrame = false;
        }

        public override void OnAcceptKeyPressed()
        {
            if (GUI.GetNameOfFocusedControl() == "xpathTextField") { DoXPathSearch(); }
            if (GUI.GetNameOfFocusedControl() == "xmlDocSearchTextField") { DoFindNext(1); }
            base.OnAcceptKeyPressed();
        }

        // Drawing Methods

        private void DrawXPathSearch(Rect inRect)
        {
            Rect xpathSearchButtonRect = inRect.RightPartPixels(128f);
            Rect xpathTextFieldRect = inRect.LeftPartPixels(inRect.width - xpathSearchButtonRect.width - 4f);
            GUI.SetNextControlName("xpathTextField");
            xpath = Widgets.TextField(xpathTextFieldRect, xpath);
            if (isFirstFrame)
                GUI.FocusControl("xpathTextField");
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
                prePatchDataLabel = $"{RichText.PrepareDataSizeLabel(XmlDocumentManager.prePatchSize)} total";
                postPatchDataLabel = $"{RichText.PrepareDataSizeLabel(XmlDocumentManager.postPatchSize)} total";
                postInheritanceDataLabel = $"{RichText.PrepareDataSizeLabel(XmlDocumentManager.postInheritanceSize)} total";
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
                if (searched || string.IsNullOrEmpty(searchText)) { codeViewport.ReindexSearch(searchText, ref CurrentTab()); }
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
                codeViewport.ReindexSearch(searchText, ref CurrentTab());
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

            float exportHeight = 30f;
            float sectionPadding = 4f;
            listing.GapLine(12f);
            Rect exportSectionRect = listing.GetRect(exportHeight + 2 * sectionPadding);
            CustomWidgets.DrawColoredSection(exportSectionRect, Color.white);
            if (Widgets.ButtonText(exportSectionRect.ContractedBy(sectionPadding), "Export"))
            {
                Find.WindowStack.Add(new Dialogue_Export(codeViewport.CurrentFormattedXml, ref CurrentTab()));
            }
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
                GUI.SetNextControlName("xmlDocSearchTextField");
                searchText = Widgets.TextField(searchboxRect, searchText);
                if (prevCurrentSearchText != searchText) { CurrentTab().needsIndexing = true; }

                findButtonsRect.TrimTopPartPixels(buttonGapSize).SplitVerticallyWithMargin(out Rect prevButtonRect, out Rect nextButtonRect, buttonGapSize);
                if (Widgets.ButtonText(prevButtonRect, "Previous"))
                    DoFindNext(-1);
                if (Widgets.ButtonText(nextButtonRect, "Next"))
                    DoFindNext(1);
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
                
                DoXPathSearch(XmlDocumentManager.prePatchDocument, ref prePatchTabData);
                DoXPathSearch(XmlDocumentManager.postPatchDocument, ref postPatchTabData);
                DoXPathSearch(XmlDocumentManager.postInheritanceDocument, ref postInheritanceTabData);

                errorXpath = false;
            }
            catch
            {
                errorXpath = true;
                prePatchTabData.resultNodeList = null;
                postPatchTabData.resultNodeList = null;
                postInheritanceTabData.resultNodeList = null;
            }
            codeViewport.pendingJumpLine = null;
            codeViewport.pendingJumpX = null;

            prePatchTabData.xpathSize = XmlDocumentManager.ComputeByteCount(prePatchTabData.resultNodeList);
            postPatchTabData.xpathSize = XmlDocumentManager.ComputeByteCount(postPatchTabData.resultNodeList);
            postInheritanceTabData.xpathSize = XmlDocumentManager.ComputeByteCount(postInheritanceTabData.resultNodeList);
            UpdateCurrentResults();

            codeViewport.ResetSearch(ref CurrentTab());
            //if (CurrentTab().hasSearched || string.IsNullOrEmpty(searchText)) { codeViewport.ReindexSearch(searchText, ref CurrentTab()); }

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

        private void UpdateCurrentResults()
        {
            if (CurrentTab().resultNodeList == null || CurrentTab().resultNodeList.Count == 0) return;
            if (CurrentTab().selectedIndex > 0 && CurrentTab().resultNodeList[CurrentTab().selectedIndex - 1] != null)
                codeViewport.SetNodesToDraw([CurrentTab().resultNodeList[CurrentTab().selectedIndex - 1]], CurrentTab());
            else if (CurrentTab().selectedIndex == 0) codeViewport.SetNodesToDraw(CurrentTab().resultNodeList.ToList(), CurrentTab());
            indexSelectorBuffer = CurrentTab().selectedIndex.ToString();
        }

        void DoFindNext(int increment)
        {
            bool reIndexed = false;
            if (CurrentTab().needsIndexing)
            {
                codeViewport.ReindexSearch(searchText, ref CurrentTab());
                reIndexed = true;
            }
            List<MatchSpan> matchList = CurrentTab().matches;
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
                    codeViewport.QueueJumpTo(matchList[idx]);
                }
                else
                    idx = -1;
                CurrentTab().activeMatch = idx;
            }
        }
    }
}
