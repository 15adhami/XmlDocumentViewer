using System.Text;
using System.Xml;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    public static class RichText
    {
        // VS Code palette
        internal const int INDENT = 6; // Since RimWorld has small spaces, we use 6 isntead of 4
        static readonly Color TagColor = new(51 / 255f, 153 / 255f, 255 / 255f);
        static readonly Color AttrColor = new(156 / 255f, 220 / 255f, 254 / 255f);
        static readonly Color ValColor = new(206 / 255f, 145 / 255f, 120 / 255f);
        static readonly Color TextColor = new(255 / 255f, 255 / 255f, 255 / 255f);
        static readonly Color CommColor = new(106 / 255f, 153 / 255f, 85 / 255f);
        static readonly Color PuncColor = new(0.65f, 0.65f, 0.65f); // <  >  </  />  =

        internal static Regex colorTagRegex = new(@"</?color(?:=[^>]*)?>", RegexOptions.IgnoreCase);
        internal static Regex LeadingSpacesRegex = new(@"^ +", RegexOptions.Multiline);

        internal static string StripColorTags(string str) => str == null ? "" : colorTagRegex.Replace(str, string.Empty);

        internal static string PrepareIndentForCopy(string str)
        {
            if (str == null) return "";
            return LeadingSpacesRegex.Replace(str, m =>
            {
                int len = m.Value.Length;
                int levels = len / INDENT;
                int rem = len % INDENT;
                return new string(' ', levels * 4 + rem);
            });
        }

        public static string ColorizeXml(XmlNode nodes)
        {
            int maxDepth = 64;
            StringBuilder stringBuilder = new(4096);
            Append(nodes, 0);
            return stringBuilder.ToString();

            void Append(XmlNode node, int depth)
            {
                if (node == null) return;

                if (node.NodeType == XmlNodeType.Comment)
                {
                    AppendLines(stringBuilder, "<!-- " + (node.Value ?? "") + " -->", depth, CommColor);
                    return;
                }

                if (node.NodeType == XmlNodeType.Text)
                {
                    AppendLines(stringBuilder, node.InnerText ?? "", depth, TextColor);
                    return;
                }

                if (node.NodeType == XmlNodeType.Element)
                {
                    XmlElement elem = (XmlElement)node;

                    stringBuilder.Append(Indent(depth))
                        .Append("<".Colorize(PuncColor))
                        .Append(elem.Name.Colorize(TagColor));

                    if (elem.HasAttributes)
                    {
                        foreach (XmlAttribute a in elem.Attributes)
                        {
                            stringBuilder.Append(" ")
                              .Append(a.Name.Colorize(AttrColor))
                              .Append("=".Colorize(TextColor))              // Color the equals?
                              .Append(("\"" + a.Value + "\"").Colorize(ValColor));
                        }
                    }

                    if (!elem.HasChildNodes)
                    {
                        stringBuilder.Append(" ")
                          .Append("/".Colorize(PuncColor))
                          .AppendLine(">".Colorize(PuncColor));
                        return;
                    }

                    if (elem.ChildNodes.Count == 1 && elem.FirstChild.NodeType == XmlNodeType.Text)
                    {
                        string t = elem.InnerText ?? "";
                        bool multiline = t.IndexOf('\n') >= 0 || t.IndexOf('\r') >= 0;

                        if (!multiline)
                        {
                            stringBuilder.Append(">".Colorize(PuncColor))
                              .Append(t.Colorize(TextColor))
                              .Append("<".Colorize(PuncColor))
                              .Append("/".Colorize(PuncColor))
                              .Append(elem.Name.Colorize(TagColor))
                              .AppendLine(">".Colorize(PuncColor));
                        }
                        else
                        {
                            stringBuilder.AppendLine(">".Colorize(PuncColor));
                            AppendLines(stringBuilder, t, depth + 1, TextColor);
                            stringBuilder.Append(Indent(depth))
                              .Append("<".Colorize(PuncColor))
                              .Append("/".Colorize(PuncColor))
                              .Append(elem.Name.Colorize(TagColor))
                              .AppendLine(">".Colorize(PuncColor));
                        }
                        return;
                    }

                    stringBuilder.AppendLine(">".Colorize(PuncColor));
                    if (depth + 1 >= maxDepth)
                    {
                        AppendLines(stringBuilder, "...", depth + 1, CommColor);
                    }
                    else
                    {
                        foreach (XmlNode child in elem.ChildNodes)
                        {
                            Append(child, depth + 1);
                        } 
                    }

                    stringBuilder.Append(Indent(depth))
                      .Append("<".Colorize(PuncColor))
                      .Append("/".Colorize(PuncColor))
                      .Append(elem.Name.Colorize(TagColor))
                      .AppendLine(">".Colorize(PuncColor));
                }
            }

            static string Indent(int d) => d <= 0 ? "" : new string(' ', d * INDENT);

            static void AppendLines(StringBuilder stringBuilder, string text, int depth, Color color)
            {
                if (text == null) return;
                text = text.Replace("\r\n", "\n").Replace("\r", "\n");

                string indent = Indent(depth);
                int i = 0;
                while (true)
                {
                    int newLineIndex = text.IndexOf('\n', i);
                    if (newLineIndex < 0)
                    {
                        stringBuilder.Append(indent).AppendLine(text.Substring(i).Colorize(color));
                        break;
                    }
                    stringBuilder.Append(indent).AppendLine(text.Substring(i, newLineIndex - i).Colorize(color));
                    i = newLineIndex + 1;
                }
            }
        }
    }
}
