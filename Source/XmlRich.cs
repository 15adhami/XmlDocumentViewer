using System.Text;
using System.Xml;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    static class XmlRich
    {
        
        static readonly Color TagC = new(80 / 255f, 80 / 255f, 255 / 255f); // tag names
        static readonly Color AttrC = new(160 / 255f, 160 / 255f, 255 / 255f); // attribute names
        static readonly Color ValC = new(255 / 255f, 153 / 255f, 51 / 255f); // attribute values
        static readonly Color TextC = new(255 / 255f, 255 / 255f, 255 / 255f); // text node
        static readonly Color CommC = new(76 / 255f, 153 / 255f, 0 / 255f); // comments
         
        /*
        static readonly Color TagC = new Color(0.55f, 0.55f, 1.00f); // tag names
        static readonly Color AttrC = new Color(0.95f, 0.85f, 0.55f); // attribute names
        static readonly Color ValC = new Color(0.60f, 1.00f, 0.70f); // attribute values
        static readonly Color TextC = new Color(0.85f, 0.85f, 0.85f); // text node
        static readonly Color CommC = new Color(0.60f, 0.85f, 0.60f); // comments*/
        const string INDENT = "    ";

        public static string ColorizeXml(XmlNode node, int maxDepth = 64, int maxChildrenPerNode = int.MaxValue, int maxTextLen = int.MaxValue)
        {
            var sb = new StringBuilder(4096);
            Append(node, 0);
            return sb.ToString();

            void Append(XmlNode n, int depth)
            {
                if (n == null) return;

                // Comment
                if (n.NodeType == XmlNodeType.Comment)
                {
                    sb.AppendLine(Indent(depth) + ("<!-- " + n.Value + " -->").Colorize(CommC));
                    return;
                }

                // Text only
                if (n.NodeType == XmlNodeType.Text) //  || n.IsTextOnly()
                {
                    string t = n.InnerText;
                    if (t.Length > maxTextLen) t = t.Substring(0, maxTextLen) + "…";
                    sb.AppendLine(Indent(depth) + t.Colorize(TextC));
                    return;
                }

                // Element
                if (n.NodeType == XmlNodeType.Element)
                {
                    var elem = (XmlElement)n;
                    // open tag
                    sb.Append(Indent(depth))
                      .Append("<")
                      .Append(elem.Name.Colorize(TagC));

                    if (elem.HasAttributes)
                    {
                        foreach (XmlAttribute a in elem.Attributes)
                        {
                            sb.Append(" ")
                              .Append(a.Name.Colorize(AttrC))
                              .Append("=")
                              .Append(("\"" + a.Value + "\"").Colorize(ValC));
                        }
                    }

                    if (!elem.HasChildNodes)
                    {
                        sb.AppendLine(" />");
                        return;
                    }

                    // has children
                    // Special case: single text child => inline
                    if (elem.ChildNodes.Count == 1 && elem.FirstChild.NodeType == XmlNodeType.Text)
                    {
                        string t = elem.InnerText;
                        if (t.Length > maxTextLen) t = t.Substring(0, maxTextLen) + "…";
                        sb.Append(">")
                          .Append(t.Colorize(TextC))
                          .Append("</")
                          .Append(elem.Name.Colorize(TagC))
                          .AppendLine(">");
                        return;
                    }

                    sb.AppendLine(">");

                    if (depth + 1 >= maxDepth)
                    {
                        sb.AppendLine(Indent(depth + 1) + "...".Colorize(CommC));
                    }
                    else
                    {
                        int shown = 0;
                        foreach (XmlNode c in elem.ChildNodes)
                        {
                            if (shown++ >= maxChildrenPerNode)
                            {
                                sb.AppendLine(Indent(depth + 1) + "...".Colorize(CommC));
                                break;
                            }
                            Append(c, depth + 1);
                        }
                    }

                    // close tag
                    sb.Append(Indent(depth))
                      .Append("</")
                      .Append(elem.Name.Colorize(TagC))
                      .AppendLine(">");
                }
            }

            static string Indent(int d)
            {
                // simple cached indent builder
                if (d <= 0) return string.Empty;
                return new string(' ', d * 4);
            }
        }
    }

}
