using RimWorld;
using System.Text;
using System.Xml;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    public static class CustomWidgets
    {
        public static void DrawColoredSection(Rect inRect, Color color, int borderThickness = 1)
        {
            GUI.color = Widgets.MenuSectionBGFillColor * color;
            GUI.DrawTexture(inRect, BaseContent.WhiteTex);
            GUI.color = new ColorInt(135, 135, 135).ToColor * color;
            Widgets.DrawBox(inRect, borderThickness, null);
            GUI.color = Color.white;
        }
    }
}
