using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
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

        public static string PrepareXml(List<XmlNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return "";

            // Build a flat list with comment nodes interleaved.
            // No artificial parent element needed.
            List<XmlNode> withComments = new List<XmlNode>(nodes.Count * 2);
            int total = nodes.Count;
            bool addComments = false;
            if (total > 1) addComments = true;
            for (int i = 0; i < total; i++)
            {
                var n = nodes[i];
                if (n == null) continue;

                XmlDocument doc = n.OwnerDocument ?? (n as XmlDocument) ?? new XmlDocument();
                if (addComments) { withComments.Add(doc.CreateComment($" Node {i + 1} / {total} ")); }
                withComments.Add(n);
            }

            const int maxDepth = 64;
            var sb = new StringBuilder(4096);

            void Append(XmlNode node, int depth)
            {
                if (node == null) return;

                if (node.NodeType == XmlNodeType.Comment)
                {
                    // Append one blank line before the comment
                    if (addComments) {sb.AppendLine(); }
                    AppendLines(sb, "<!-- " + (node.Value ?? "") + " -->", depth, CommColor);
                    return;
                }

                if (node.NodeType == XmlNodeType.Text)
                {
                    AppendLines(sb, node.InnerText ?? "", depth, TextColor);
                    return;
                }

                if (node.NodeType == XmlNodeType.Element)
                {
                    var elem = (XmlElement)node;

                    sb.Append(Indent(depth))
                      .Append("<".Colorize(PuncColor))
                      .Append(elem.Name.Colorize(TagColor));

                    if (elem.HasAttributes)
                    {
                        foreach (XmlAttribute a in elem.Attributes)
                        {
                            sb.Append(" ")
                              .Append(a.Name.Colorize(AttrColor))
                              .Append("=".Colorize(PuncColor))
                              .Append(("\"" + a.Value + "\"").Colorize(ValColor));
                        }
                    }

                    if (!elem.HasChildNodes)
                    {
                        sb.Append(" ")
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
                            sb.Append(">".Colorize(PuncColor))
                              .Append(t.Colorize(TextColor))
                              .Append("<".Colorize(PuncColor))
                              .Append("/".Colorize(PuncColor))
                              .Append(elem.Name.Colorize(TagColor))
                              .AppendLine(">".Colorize(PuncColor));
                        }
                        else
                        {
                            sb.AppendLine(">".Colorize(PuncColor));
                            AppendLines(sb, t, depth + 1, TextColor);
                            sb.Append(Indent(depth))
                              .Append("<".Colorize(PuncColor))
                              .Append("/".Colorize(PuncColor))
                              .Append(elem.Name.Colorize(TagColor))
                              .AppendLine(">".Colorize(PuncColor));
                        }
                        return;
                    }

                    sb.AppendLine(">".Colorize(PuncColor));
                    if (depth + 1 >= maxDepth)
                    {
                        AppendLines(sb, "...", depth + 1, CommColor);
                    }
                    else
                    {
                        foreach (XmlNode child in elem.ChildNodes)
                            Append(child, depth + 1);
                    }

                    sb.Append(Indent(depth))
                      .Append("<".Colorize(PuncColor))
                      .Append("/".Colorize(PuncColor))
                      .Append(elem.Name.Colorize(TagColor))
                      .AppendLine(">".Colorize(PuncColor));
                }
            }

            foreach (XmlNode n in withComments)
                Append(n, 0);

            // Trim a leading newline if the first node was a comment.
            while (sb.Length > 0 && (sb[0] == '\n' || sb[0] == '\r'))
                sb.Remove(0, 1);

            return sb.ToString();
        }

        // helpers already in your class:
        static string Indent(int d) => d <= 0 ? "" : new string(' ', d * INDENT);

        static void AppendLines(StringBuilder sb, string text, int depth, Color color)
        {
            if (text == null) return;
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            string indent = Indent(depth);
            int i = 0;
            while (true)
            {
                int nl = text.IndexOf('\n', i);
                if (nl < 0)
                {
                    sb.Append(indent).AppendLine(text.Substring(i).Colorize(color));
                    break;
                }
                sb.Append(indent).AppendLine(text.Substring(i, nl - i).Colorize(color));
                i = nl + 1;
            }
        }

        public static string PrependIndexComment(string formatted, int index, int total)
        {
            string header = ("<!-- Node " + index + " / " + total + " -->").Colorize(CommColor);
            return header + "\n" + formatted;
        }

        internal static string PrepareDataSizeLabel(int bytes)
        {
            float KB = 1024;
            float MB = KB * 1024;
            float GB = MB * 1024;

            string preparedLabel;
            if (bytes < KB) { preparedLabel = bytes.ToString() + " B"; }
            else if (bytes < MB) { preparedLabel = (bytes / KB).ToString("F2") + " KB"; }
            else if (bytes < GB) { preparedLabel = (bytes / MB).ToString("F2") + " MB"; }
            else { preparedLabel = (bytes / GB).ToString("F2") + " GB"; }

            return preparedLabel;
        }

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
    }
}
