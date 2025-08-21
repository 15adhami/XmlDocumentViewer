namespace XmlDocumentViewer
{
    public struct MatchSpan
    {
        public int line;
        public int start;
        public int length;

        public float x;       // left (content coords, includes left padding)
        public float w;       // width of match (content coords)
        public float normY;   // normalized center Y in [0..1] for scrollbar
    }
}
