/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Eto.Drawing;

namespace SmartHopper.Core.Converters
{
    /// <summary>
    /// Provides markdown parsing and formatting utilities
    /// </summary>
    public static class Markdown
    {
        ///// <summary>
        ///// Converts markdown text to HTML
        ///// </summary>
        ///// <param name="markdown">The markdown text to convert</param>
        ///// <returns>HTML representation of the markdown</returns>
        //public static string ToHtml(string markdown)
        //{
        //    if (string.IsNullOrEmpty(markdown))
        //        return string.Empty;

        //    // Replace code blocks
        //    var codeBlockPattern = @"```(.*?)```";
        //    markdown = Regex.Replace(
        //        markdown,
        //        codeBlockPattern,
        //        m => $"<pre><code>{m.Groups[1].Value}</code></pre>",
        //        RegexOptions.Singleline
        //    );

        //    // Replace inline code
        //    markdown = Regex.Replace(markdown, @"`([^`]+)`", "<code>$1</code>");

        //    // Replace headers
        //    markdown = Regex.Replace(markdown, @"^# (.*?)$", "<h1>$1</h1>", RegexOptions.Multiline);
        //    markdown = Regex.Replace(markdown, @"^## (.*?)$", "<h2>$1</h2>", RegexOptions.Multiline);
        //    markdown = Regex.Replace(markdown, @"^### (.*?)$", "<h3>$1</h3>", RegexOptions.Multiline);
        //    markdown = Regex.Replace(markdown, @"^#### (.*?)$", "<h4>$1</h4>", RegexOptions.Multiline);
        //    markdown = Regex.Replace(markdown, @"^##### (.*?)$", "<h5>$1</h5>", RegexOptions.Multiline);
        //    markdown = Regex.Replace(markdown, @"^###### (.*?)$", "<h6>$1</h6>", RegexOptions.Multiline);

        //    // Replace bold
        //    markdown = Regex.Replace(markdown, @"\*\*(.*?)\*\*", "<strong>$1</strong>");
        //    markdown = Regex.Replace(markdown, @"__(.*?)__", "<strong>$1</strong>");

        //    // Replace italic
        //    markdown = Regex.Replace(markdown, @"\*(.*?)\*", "<em>$1</em>");
        //    markdown = Regex.Replace(markdown, @"_(.*?)_", "<em>$1</em>");

        //    // Replace blockquotes
        //    markdown = Regex.Replace(markdown, @"^> (.*?)$", "<blockquote>$1</blockquote>", RegexOptions.Multiline);

        //    // Replace links
        //    markdown = Regex.Replace(markdown, @"\[(.*?)\]\((.*?)\)", "<a href=\"$2\">$1</a>");

        //    // Replace lists
        //    markdown = Regex.Replace(markdown, @"^\* (.*?)$", "<ul><li>$1</li></ul>", RegexOptions.Multiline);
        //    markdown = Regex.Replace(markdown, @"^- (.*?)$", "<ul><li>$1</li></ul>", RegexOptions.Multiline);
        //    markdown = Regex.Replace(markdown, @"^(\d+)\. (.*?)$", "<ol><li>$2</li></ol>", RegexOptions.Multiline);

        //    // Replace consecutive </ul><ul> with just a line break
        //    markdown = markdown.Replace("</ul>\r\n<ul>", "\r\n");
        //    markdown = markdown.Replace("</ul>\n<ul>", "\n");

        //    // Replace consecutive </ol><ol> with just a line break
        //    markdown = markdown.Replace("</ol>\r\n<ol>", "\r\n");
        //    markdown = markdown.Replace("</ol>\n<ol>", "\n");

        //    // Replace line breaks with <br>
        //    markdown = Regex.Replace(markdown, @"\r\n|\n", "<br>");

        //    return markdown;
        //}

        /// <summary>
        /// Represents a segment of text with formatting information
        /// </summary>
        public class TextSegment
        {
            public string Text { get; set; }
            public Font Font { get; set; }
            public Color Color { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public bool IsLineBreak { get; set; }
            public bool HasBackground { get; set; }
            public Color BackgroundColor { get; set; }
            public bool IsBlockquote { get; set; }
        }

        /// <summary>
        /// Represents an inline formatting span
        /// </summary>
        public class InlineFormatSpan
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public string Text { get; set; }
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
            public bool IsCode { get; set; }
        }

        /// <summary>
        /// Processes markdown text into formatted segments
        /// </summary>
        /// <param name="markdown">The markdown text to process</param>
        /// <param name="availableWidth">The available width for text layout</param>
        /// <param name="defaultFont">The default font to use for unformatted text</param>
        /// <param name="defaultColor">The default color to use for unformatted text</param>
        /// <param name="graphics">The graphics context for measuring text</param>
        /// <returns>A list of formatted text segments</returns>
        public static List<TextSegment> ProcessMarkdown(string markdown, int availableWidth, Font defaultFont, Color defaultColor, Graphics graphics)
        {
            var segments = new List<TextSegment>();
            
            if (string.IsNullOrEmpty(markdown))
                return segments;

            // Process headings
            markdown = ProcessHeadings(markdown, defaultFont, defaultColor, segments);
            
            // Process code blocks
            markdown = ProcessCodeBlocks(markdown, defaultFont, defaultColor, segments);
            
            // Process remaining lines with inline formatting
            string[] lines = markdown.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    // Add an empty line
                    segments.Add(new TextSegment { Text = string.Empty, Font = defaultFont, Color = defaultColor, IsLineBreak = true });
                    continue;
                }
                
                // Process inline formatting
                ProcessInlineFormatting(line, availableWidth, defaultFont, defaultColor, graphics, segments);
            }
            
            return segments;
        }

        /// <summary>
        /// Processes headings in markdown
        /// </summary>
        private static string ProcessHeadings(string markdown, Font defaultFont, Color defaultColor, List<TextSegment> segments)
        {
            var headingRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);
            var result = headingRegex.Replace(markdown, match => {
                int level = match.Groups[1].Length;
                string headingText = match.Groups[2].Value;
                
                // Create a font for the heading based on level
                float fontSize = defaultFont.Size + (6 - level);
                var headingFont = new Font(defaultFont.Family, fontSize, FontStyle.Bold);
                
                // Add the heading as a segment
                segments.Add(new TextSegment { 
                    Text = headingText, 
                    Font = headingFont, 
                    Color = defaultColor,
                    IsLineBreak = true 
                });
                
                // Return empty string to remove the heading from further processing
                return "";
            });
            
            return result;
        }

        /// <summary>
        /// Processes code blocks in markdown
        /// </summary>
        private static string ProcessCodeBlocks(string markdown, Font defaultFont, Color defaultColor, List<TextSegment> segments)
        {
            var codeBlockRegex = new Regex(@"```(.*?)```", RegexOptions.Singleline);
            var result = codeBlockRegex.Replace(markdown, match => {
                string codeContent = match.Groups[1].Value.Trim();
                string[] codeLines = codeContent.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
                
                // Use a monospace font for code
                var codeFont = new Font(FontFamilies.Monospace, defaultFont.Size);
                
                // Add a line break before code block
                segments.Add(new TextSegment { Text = string.Empty, Font = defaultFont, Color = defaultColor, IsLineBreak = true });
                
                // Add each line of code
                foreach (string line in codeLines)
                {
                    segments.Add(new TextSegment { 
                        Text = line, 
                        Font = codeFont, 
                        Color = Colors.DarkGray,
                        IsLineBreak = true,
                        BackgroundColor = Colors.LightGrey,
                        HasBackground = true
                    });
                }
                
                // Add a line break after code block
                segments.Add(new TextSegment { Text = string.Empty, Font = defaultFont, Color = defaultColor, IsLineBreak = true });
                
                // Return empty string to remove the code block from further processing
                return "";
            });
            
            return result;
        }

        /// <summary>
        /// Processes inline formatting in markdown
        /// </summary>
        private static void ProcessInlineFormatting(string line, int availableWidth, Font defaultFont, Color defaultColor, Graphics graphics, List<TextSegment> segments)
        {
            // Process blockquotes
            if (line.StartsWith(">"))
            {
                string quoteText = line.Substring(1).Trim();
                segments.Add(new TextSegment { 
                    Text = quoteText, 
                    Font = defaultFont, 
                    Color = Colors.DarkGray,
                    IsLineBreak = true,
                    IsBlockquote = true
                });
                return;
            }
            
            // Process inline formatting
            int currentIndex = 0;
            List<InlineFormatSpan> formatSpans = FindFormatSpans(line);
            
            if (formatSpans.Count == 0)
            {
                // No formatting, just add the line as is
                if (graphics.MeasureString(defaultFont, line).Width <= availableWidth)
                {
                    segments.Add(new TextSegment { Text = line, Font = defaultFont, Color = defaultColor, IsLineBreak = true });
                }
                else
                {
                    WrapTextIntoSegments(line, availableWidth, graphics, defaultFont, defaultColor, segments);
                }
                return;
            }
            
            // Process text with inline formatting
            foreach (var span in formatSpans.OrderBy(s => s.StartIndex))
            {
                // Add text before this format span
                if (span.StartIndex > currentIndex)
                {
                    string beforeText = line.Substring(currentIndex, span.StartIndex - currentIndex);
                    segments.Add(new TextSegment { Text = beforeText, Font = defaultFont, Color = defaultColor });
                }
                
                // Add the formatted text
                Font spanFont = defaultFont;
                if (span.IsBold)
                {
                    spanFont = new Font(defaultFont.Family, defaultFont.Size, FontStyle.Bold);
                }
                else if (span.IsItalic)
                {
                    spanFont = new Font(defaultFont.Family, defaultFont.Size, FontStyle.Italic);
                }
                else if (span.IsCode)
                {
                    spanFont = new Font(FontFamilies.Monospace, defaultFont.Size);
                }
                
                segments.Add(new TextSegment { 
                    Text = span.Text, 
                    Font = spanFont, 
                    Color = span.IsCode ? Colors.DarkGray : defaultColor,
                    BackgroundColor = span.IsCode ? Colors.LightGrey : Colors.Transparent,
                    HasBackground = span.IsCode
                });
                
                currentIndex = span.EndIndex;
            }
            
            // Add any remaining text
            if (currentIndex < line.Length)
            {
                string remainingText = line.Substring(currentIndex);
                segments.Add(new TextSegment { Text = remainingText, Font = defaultFont, Color = defaultColor, IsLineBreak = true });
            }
            else
            {
                // Ensure line break at the end
                segments[segments.Count - 1].IsLineBreak = true;
            }
        }

        /// <summary>
        /// Finds inline formatting spans in text
        /// </summary>
        private static List<InlineFormatSpan> FindFormatSpans(string text)
        {
            var spans = new List<InlineFormatSpan>();
            
            // Find bold text (**text**)
            var boldRegex = new Regex(@"\*\*(.+?)\*\*");
            foreach (Match match in boldRegex.Matches(text))
            {
                spans.Add(new InlineFormatSpan {
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Text = match.Groups[1].Value,
                    IsBold = true
                });
            }
            
            // Find italic text (*text*)
            var italicRegex = new Regex(@"\*(.+?)\*");
            foreach (Match match in italicRegex.Matches(text))
            {
                // Skip if this is part of a bold match
                bool isPartOfBold = spans.Any(s => s.IsBold && 
                    ((match.Index >= s.StartIndex && match.Index < s.EndIndex) ||
                     (match.Index + match.Length > s.StartIndex && match.Index + match.Length <= s.EndIndex)));
                
                if (!isPartOfBold)
                {
                    spans.Add(new InlineFormatSpan {
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Text = match.Groups[1].Value,
                        IsItalic = true
                    });
                }
            }
            
            // Find inline code (`text`)
            var codeRegex = new Regex(@"`(.+?)`");
            foreach (Match match in codeRegex.Matches(text))
            {
                spans.Add(new InlineFormatSpan {
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Text = match.Groups[1].Value,
                    IsCode = true
                });
            }
            
            return spans;
        }

        ///// <summary>
        ///// Processes plain text into segments
        ///// </summary>
        //public static void ProcessPlainText(string text, int availableWidth, Font defaultFont, Color defaultColor, Graphics graphics, List<TextSegment> segments)
        //{
        //    string[] lines = text.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            
        //    foreach (string line in lines)
        //    {
        //        if (string.IsNullOrEmpty(line))
        //        {
        //            // Add an empty line
        //            segments.Add(new TextSegment { Text = string.Empty, Font = defaultFont, Color = defaultColor, IsLineBreak = true });
        //            continue;
        //        }

        //        // Measure the text to see if it needs to be wrapped
        //        SizeF textSize = graphics.MeasureString(defaultFont, line);
                
        //        if (textSize.Width <= availableWidth)
        //        {
        //            // The line fits, no need to wrap
        //            segments.Add(new TextSegment { Text = line, Font = defaultFont, Color = defaultColor, IsLineBreak = true });
        //        }
        //        else
        //        {
        //            // Need to wrap the text
        //            WrapTextIntoSegments(line, availableWidth, graphics, defaultFont, defaultColor, segments);
        //        }
        //    }
        //}

        /// <summary>
        /// Wraps text into multiple segments based on available width
        /// </summary>
        private static void WrapTextIntoSegments(string text, int availableWidth, Graphics graphics, Font font, Color color, List<TextSegment> segments)
        {
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder currentLine = new StringBuilder();
            
            foreach (string word in words)
            {
                string testLine = currentLine.ToString();
                if (testLine.Length > 0)
                    testLine += " ";
                testLine += word;
                
                SizeF testSize = graphics.MeasureString(font, testLine);
                
                if (testSize.Width <= availableWidth)
                {
                    // Word fits on the current line
                    if (currentLine.Length > 0)
                        currentLine.Append(" ");
                    currentLine.Append(word);
                }
                else
                {
                    // Word doesn't fit, create a new line
                    if (currentLine.Length > 0)
                    {
                        segments.Add(new TextSegment { Text = currentLine.ToString(), Font = font, Color = color, IsLineBreak = true });
                        currentLine.Clear();
                    }
                    
                    // If the word itself is too long, we need to break it
                    if (graphics.MeasureString(font, word).Width > availableWidth)
                    {
                        // This is a simplified approach - in a real implementation
                        // you'd want to break the word at character boundaries
                        currentLine.Append(word);
                    }
                    else
                    {
                        currentLine.Append(word);
                    }
                }
            }
            
            // Add the last line if there's anything left
            if (currentLine.Length > 0)
            {
                segments.Add(new TextSegment { Text = currentLine.ToString(), Font = font, Color = color, IsLineBreak = true });
            }
        }

        /// <summary>
        /// Calculates the layout of text segments
        /// </summary>
        public static void CalculateSegmentLayout(List<TextSegment> segments, int width, int padding, Graphics graphics)
        {
            float y = padding;
            float x = padding;
            float lineHeight = 0;
            
            foreach (var segment in segments)
            {
                SizeF segmentSize = graphics.MeasureString(segment.Font, segment.Text);
                
                // If this segment would exceed the width, move to the next line
                if (x + segmentSize.Width > width - padding && x > padding)
                {
                    y += lineHeight;
                    x = padding;
                    lineHeight = 0;
                }
                
                // Update segment position
                segment.X = x;
                segment.Y = y;
                
                // Update position for next segment
                x += segmentSize.Width;
                
                // Update line height if this segment is taller
                lineHeight = Math.Max(lineHeight, segmentSize.Height);
                
                // If this is a line break, move to the next line
                if (segment.IsLineBreak)
                {
                    y += lineHeight;
                    x = padding;
                    lineHeight = 0;
                }
            }
        }
    }
}
