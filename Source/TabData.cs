using System.Collections.Generic;
using System.Xml;
using UnityEngine;

namespace XmlDocumentViewer
{
    public struct TabData
    {
        //  Menu fields
        internal Vector2 scrollPos = Vector2.zero;
        internal XmlDocument xmlDocument;
        internal XmlNodeList resultNodeList;
        internal int selectedIndex = 0;
        internal int xpathSize = 0;
        internal int timer = 0;
        internal DocumentType documentType;

        // Search fields
        internal List<MatchSpan> matches = [];
        internal bool needsIndexing = true;
        internal bool hasSearched = false;
        internal int activeMatch = -1;

        internal enum DocumentType
        {
            PrePatch,
            PostPatch,
            PostInheritance
        }

        public TabData() { }

        /// <summary>
        /// Clears all data except hasSearched
        /// </summary>
        public void ClearAll()
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

        public List<XmlNode> GetCurrentNodeList()
        {
            if (resultNodeList == null || resultNodeList.Count == 0) return [];
            if (selectedIndex > 0 && resultNodeList[selectedIndex - 1] != null)
                return [resultNodeList[selectedIndex - 1]];
            else if (selectedIndex == 0) resultNodeList.ToList();
            return [];
        }
    }
}
