using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    internal static class RectExtensions
    {
        internal static Rect TrimLeftPart(this Rect rect, float pct)
        {
            return rect.RightPart(1f - pct);
        }

        internal static Rect TrimRightPart(this Rect rect, float pct)
        {
            return rect.LeftPart(1f - pct);
        }

        internal static Rect TrimTopPart(this Rect rect, float pct)
        {
            return rect.BottomPart(1f - pct);
        }

        internal static Rect TrimBottomPart(this Rect rect, float pct)
        {
            return rect.TopPart(1f - pct);
        }

        internal static Rect TrimLeftPartPixels(this Rect rect, float pixels)
        {
            return rect.RightPartPixels(rect.width - pixels);
        }

        internal static Rect TrimRightPartPixels(this Rect rect, float pixels)
        {
            return rect.LeftPartPixels(rect.width - pixels);
        }

        internal static Rect TrimTopPartPixels(this Rect rect, float pixels)
        {
            return rect.BottomPartPixels(rect.height - pixels);
        }

        internal static Rect TrimBottomPartPixels(this Rect rect, float pixels)
        {
            return rect.TopPartPixels(rect.height - pixels);
        }
    }
}
