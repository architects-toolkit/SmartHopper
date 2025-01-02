/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using HtmlAgilityPack;
using Markdig;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SmartHopper.Core.Converters
{
    public static class FormatConverter
    {
        public static string ConvertMarkdownToRtf(string markdown)
        {
            var pipeline = new MarkdownPipelineBuilder().Build();
            var html = Markdig.Markdown.ToHtml(markdown, pipeline);

            html = "<html><body>" + html + "</body></html>";

            var rtfBody = ConvertHtmlToRtf(html);

            var rtfHeader = "{\\rtf1\\ansi\\deff0 {\\fonttbl {\\f0 Segoe UI;}}\\fs19\\cf1 {\\colortbl;\\red255\\green255\\blue255;}\n";
            var rtfFooter = "}";

            return rtfHeader + rtfBody + rtfFooter;
        }

        private static string ConvertNodeToRtf(HtmlNode node, StringBuilder rtf)
        {
            if (node == null) return string.Empty;

            switch (node.NodeType)
            {
                case HtmlNodeType.Text:
                    return node.InnerText;
                case HtmlNodeType.Element:
                    switch (node.Name.ToLowerInvariant())
                    {
                        case "strong":
                        case "b":
                            return $"\\b {node.InnerText?.Trim() ?? ""}\\b0 ";
                        case "em":
                        case "i":
                            return $"\\i {node.InnerText?.Trim() ?? ""}\\i0 ";
                        case "code":
                            return $"\\highlight15\\f1\\fs18 {node.InnerText?.Trim() ?? ""}\\highlight0\\f0\\fs20 ";
                        default:
                            return node.InnerText?.Trim() ?? "";
                    }
                default:
                    return string.Empty;
            }
        }

        public static string ConvertHtmlToRtf(string html)
        {
            try
            {
                if (string.IsNullOrEmpty(html))
                    return "";

                const int LIST_INDENT_VALUE = 150;
                html = html.Replace("&quot;", "\\\"");

                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                if (doc.DocumentNode == null)
                    return "";

                var rtf = new StringBuilder();
                var listLevel = 0;

                foreach (var node in doc.DocumentNode.DescendantsAndSelf())
                {
                    if (node == null) continue;

                    switch (node.Name?.ToLowerInvariant())
                    {
                        case "h1":
                            rtf.Append("\\pard\\fs32\\b ").Append(ConvertNodeToRtf(node, rtf)).Append("\\b0\\par\\par ");
                            break;
                        case "h2":
                            rtf.Append("\\pard\\fs28\\b ").Append(ConvertNodeToRtf(node, rtf)).Append("\\b0\\par\\par ");
                            break;
                        case "h3":
                            rtf.Append("\\pard\\fs24\\b ").Append(ConvertNodeToRtf(node, rtf)).Append("\\b0\\par\\par ");
                            break;
                        case "p":
                            if (node.ParentNode?.Name != "li")
                            {
                                rtf.Append("\\pard\\fs20 ").Append(ConvertNodeToRtf(node, rtf)).Append("\\par\\par ");
                            }
                            break;
                        case "ol":
                        case "ul":
                            listLevel++;
                            break;
                        case "li":
                            rtf.Append("\\pard\\li").Append(listLevel * LIST_INDENT_VALUE);
                            if (node.ParentNode?.Name == "ol")
                            {
                                int index = 1;
                                var previous = node.PreviousSibling;
                                while (previous != null)
                                {
                                    if (previous.Name == "li")
                                        index++;
                                    previous = previous.PreviousSibling;
                                }
                                rtf.Append("\\fs20 ").Append(index).Append(". ");
                            }
                            else
                            {
                                rtf.Append("\\fs20 • ");
                            }

                            foreach (var childNode in node.ChildNodes)
                            {
                                if (childNode.Name == "p" && childNode != node.FirstChild)
                                {
                                    rtf.Append("\\par\\li").Append(listLevel * LIST_INDENT_VALUE).Append("   ");
                                }
                                rtf.Append(ConvertNodeToRtf(childNode, rtf));
                            }
                            rtf.Append("\\par ");
                            break;
                        case "#text":
                            if (node.ParentNode?.Name != "li" &&
                                node.ParentNode?.Name != "p" &&
                                !new[] { "strong", "b", "em", "i", "code" }.Contains(node.ParentNode?.Name))
                            {
                                var text = ConvertNodeToRtf(node, rtf);
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    rtf.Append("\\pard\\fs20 ").Append(text).Append("\\par ");
                                }
                            }
                            break;
                    }

                    if ((node.Name == "ol" || node.Name == "ul") && !node.HasChildNodes)
                    {
                        listLevel = Math.Max(0, listLevel - 1);
                    }
                    else if (node.NextSibling == null && node.ParentNode != null &&
                            (node.ParentNode.Name == "ol" || node.ParentNode.Name == "ul"))
                    {
                        listLevel = Math.Max(0, listLevel - 1);
                        rtf.Append("\\pard\\par ");
                    }
                }

                return rtf.ToString().TrimEnd(new[] { ' ', '\\', 'p', 'a', 'r' });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting HTML to RTF: {ex.Message}");
                return html;
            }
        }
    }
}
