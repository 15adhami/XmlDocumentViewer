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
        internal string searchText = "";
        internal bool needsIndexing = true;
        internal int activeMatch = -1;

        public TabData() { }

        internal void ClearData()
        {
            scrollPos = Vector2.zero;
            resultNodeList = null;
            selectedIndex = 0;
            xpathSize = 0;
            timer = 0;
            searchText = "";
            needsIndexing = true;
            matches.Clear();
            activeMatch = -1;
        }
    }
}
