using System.Collections.Generic;
using System.Xml;
using UnityEngine;

namespace XmlDocumentViewer
{
    internal struct TabData
    {
        //  Menu fields
        internal Vector2 scrollPos = Vector2.zero;
        internal XmlNodeList resultNodeList;
        internal int selectedIndex = 0;
        internal int xpathSize = 0;
        internal int timer = 0;

        // Search fields
        internal struct MatchSpan { public int line; public int start; public int length; }
        internal List<MatchSpan> matches = [];
        internal bool needsIndexing = true;
        internal bool hasSearched = false;
        internal int activeMatch = -1;

        public TabData() { }

        /// <summary>
        /// Clears all data except hasSearched
        /// </summary>
        internal void ClearAll()
        {
            scrollPos = Vector2.zero;
            resultNodeList = null;
            selectedIndex = 0;
            xpathSize = 0;
            timer = 0;
            needsIndexing = true;
            matches.Clear();
            activeMatch = -1;
        }
    }
}
