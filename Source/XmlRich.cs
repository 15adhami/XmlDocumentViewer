using System.Text;
using System.Xml;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    static class XmlRich
    {
        internal static readonly Regex colorTagRegex =
            new(@"</?color(?:=[^>]*)?>", RegexOptions.IgnoreCase);

        internal static string StripRichColorTags(string s) => s == null ? "" : colorTagRegex.Replace(s, string.Empty);

        static readonly Color TagColor = new(51 / 255f, 153 / 255f, 255 / 255f); // tag names
        static readonly Color AttrColor = new(156 / 255f, 220 / 255f, 254 / 255f); // attribute names
        static readonly Color ValColor = new(206 / 255f, 145 / 255f, 120 / 255f); // attribute values
        static readonly Color TextColor = new(255 / 255f, 255 / 255f, 255 / 255f); // text node
        static readonly Color CommColor = new(106 / 255f, 153 / 255f, 85 / 255f); // comments
        const string INDENT = "    ";

        public static string ColorizeXml(XmlNode node, int maxDepth = 64, int maxChildrenPerNode = int.MaxValue, int maxTextLen = int.MaxValue)
        {
            var sb = new StringBuilder(4096);
            Append(node, 0);
            return sb.ToString();

            void Append(XmlNode n, int depth)
            {
                if (n == null) { return; }

                // Comment
                if (n.NodeType == XmlNodeType.Comment)
                {
                    sb.AppendLine(Indent(depth) + ("<!-- " + n.Value + " -->").Colorize(CommColor));
                    return;
                }

                // Text only
                if (n.NodeType == XmlNodeType.Text)
                {
                    string t = n.InnerText;
                    if (t.Length > maxTextLen) t = t.Substring(0, maxTextLen) + "…";
                    sb.AppendLine(Indent(depth) + t.Colorize(TextColor));
                    return;
                }

                // Element
                if (n.NodeType == XmlNodeType.Element)
                {
                    var elem = (XmlElement)n;
                    sb.Append(Indent(depth)).Append("<").Append(elem.Name.Colorize(TagColor));
                    if (elem.HasAttributes)
                    {
                        foreach (XmlAttribute a in elem.Attributes)
                        {
                            sb.Append(" ")
                              .Append(a.Name.Colorize(AttrColor))
                              .Append("=")
                              .Append(("\"" + a.Value + "\"").Colorize(ValColor));
                        }
                    }

                    if (!elem.HasChildNodes)
                    {
                        sb.AppendLine(" />");
                        return;
                    }

                    // Has children
                    if (elem.ChildNodes.Count == 1 && elem.FirstChild.NodeType == XmlNodeType.Text)
                    {
                        string t = elem.InnerText;
                        if (t.Length > maxTextLen) t = t.Substring(0, maxTextLen) + "…";
                        sb.Append(">")
                          .Append(t.Colorize(TextColor))
                          .Append("</")
                          .Append(elem.Name.Colorize(TagColor))
                          .AppendLine(">");
                        return;
                    }

                    sb.AppendLine(">");

                    if (depth + 1 >= maxDepth)
                    {
                        sb.AppendLine(Indent(depth + 1) + "...".Colorize(CommColor));
                    }
                    else
                    {
                        int shown = 0;
                        foreach (XmlNode c in elem.ChildNodes)
                        {
                            if (shown++ >= maxChildrenPerNode)
                            {
                                sb.AppendLine(Indent(depth + 1) + "...".Colorize(CommColor));
                                break;
                            }
                            Append(c, depth + 1);
                        }
                    }

                    sb.Append(Indent(depth)).Append("</").Append(elem.Name.Colorize(TagColor)).AppendLine(">");
                }
            }

            static string Indent(int d)
            {
                if (d <= 0) return string.Empty;
                return new string(' ', d * 4);
            }
        }
    }

}
