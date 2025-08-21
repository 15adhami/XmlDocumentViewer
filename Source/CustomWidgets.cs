using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    public static class CustomWidgets
    {
        public static Color menuSectionBorderColor = new ColorInt(135, 135, 135).ToColor;

        public static void DrawColoredSection(Rect inRect, Color color, int borderThickness = 1)
        {
            GUI.color = Widgets.MenuSectionBGFillColor * color;
            GUI.DrawTexture(inRect, BaseContent.WhiteTex);
            GUI.color = menuSectionBorderColor * color;
            Widgets.DrawBox(inRect, borderThickness);
            GUI.color = Color.white;
        }
    }
}
