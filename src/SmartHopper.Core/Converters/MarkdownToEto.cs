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
    /// Provides utilities for converting Markdown text to Eto.Forms formatted text segments
    /// </summary>
    public static class MarkdownToEto
    {
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
            public bool IsList { get; set; }
            public bool IsOrderedList { get; set; }
            public int ListIndent { get; set; }
            public string ListMarker { get; set; }
            public bool IsLink { get; set; }
            public string Url { get; set; }
            public bool IsHorizontalRule { get; set; }
        }

        /// <summary>
        /// Represents an inline formatting span
        /// </summary>
        private class InlineFormatSpan
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public string Text { get; set; }
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
            public bool IsCode { get; set; }
            public bool IsLink { get; set; }
            public string Url { get; set; }
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

            // Split the markdown into lines while preserving line breaks
            string[] lines = markdown.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            
            // Process each line in order
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                
                if (string.IsNullOrEmpty(line))
                {
                    // Add an empty line
                    segments.Add(new TextSegment { Text = string.Empty, Font = defaultFont, Color = defaultColor, IsLineBreak = true });
                    continue;
                }
                
                // Check for heading
                var headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
                if (headingMatch.Success)
                {
                    ProcessHeadingLine(headingMatch.Groups[1].Value, headingMatch.Groups[2].Value, defaultFont, defaultColor, segments);
                    continue;
                }
                
                // Check for code block start
                if (line.Trim().StartsWith("```"))
                {
                    StringBuilder codeBlock = new StringBuilder();
                    //string language = line.Trim().Substring(3).Trim(); // Extract language identifier if present
                    i++; // Move to the next line
                    
                    // Collect all lines until the closing ```
                    while (i < lines.Length && !lines[i].Trim().Equals("```"))
                    {
                        codeBlock.AppendLine(lines[i]);
                        i++;
                    }
                    
                    // Process the code block
                    ProcessCodeBlockContent(codeBlock.ToString(), defaultFont, segments);
                    continue;
                }
                
                // Check for horizontal rule
                if (line.Trim().Equals("---") || line.Trim().Equals("***") || line.Trim().Equals("___"))
                {
                    segments.Add(new TextSegment { 
                        Text = string.Empty, 
                        Font = defaultFont, 
                        Color = defaultColor, 
                        IsLineBreak = true,
                        IsHorizontalRule = true
                    });
                    continue;
                }
                
                // Check for blockquote
                var blockquoteMatch = Regex.Match(line, @"^>\s*(.*)$");
                if (blockquoteMatch.Success)
                {
                    string quoteText = blockquoteMatch.Groups[1].Value;
                    
                    // Process the blockquote text for inline formatting
                    var quoteSegments = new List<TextSegment>();
                    ProcessInlineFormatting(quoteText, defaultFont, defaultColor, quoteSegments);
                    
                    // Add the blockquote with proper formatting
                    foreach (var segment in quoteSegments)
                    {
                        segment.IsBlockquote = true;
                        segments.Add(segment);
                    }
                    
                    // Make sure the last segment has a line break
                    if (quoteSegments.Count > 0)
                    {
                        quoteSegments[quoteSegments.Count - 1].IsLineBreak = true;
                    }
                    else
                    {
                        // Empty blockquote line (e.g., ">")
                        segments.Add(new TextSegment { 
                            Text = string.Empty, 
                            Font = defaultFont, 
                            Color = defaultColor, 
                            IsLineBreak = true,
                            IsBlockquote = true
                        });
                    }
                    
                    continue;
                }
                
                // Check for unordered list item
                var unorderedListMatch = Regex.Match(line, @"^([ \t]*)(\*|\-|\+)\s+(.+)$");
                if (unorderedListMatch.Success)
                {
                    string indent = unorderedListMatch.Groups[1].Value;
                    string marker = unorderedListMatch.Groups[2].Value;
                    string itemText = unorderedListMatch.Groups[3].Value;
                    int indentLevel = indent.Length / 2;
                    
                    ProcessListItem(itemText, indentLevel, marker, false, defaultFont, defaultColor, segments);
                    continue;
                }
                
                // Check for ordered list item
                var orderedListMatch = Regex.Match(line, @"^([ \t]*)(\d+)\.?\s+(.+)$");
                if (orderedListMatch.Success)
                {
                    string indent = orderedListMatch.Groups[1].Value;
                    string number = orderedListMatch.Groups[2].Value;
                    string itemText = orderedListMatch.Groups[3].Value;
                    int indentLevel = indent.Length / 2;
                    
                    ProcessListItem(itemText, indentLevel, number, true, defaultFont, defaultColor, segments);
                    continue;
                }
                
                // Process regular line with inline formatting
                ProcessInlineFormatting(line, defaultFont, defaultColor, segments);
            }
            
            return segments;
        }
        
        /// <summary>
        /// Processes a heading line
        /// </summary>
        private static void ProcessHeadingLine(string hashMarks, string headingText, Font defaultFont, Color defaultColor, List<TextSegment> segments)
        {
            int level = hashMarks.Length;
            float sizeFactor = Math.Max(1.0f, 2.0f - ((level - 1) * 0.2f));
            
            var headingFont = new Font(defaultFont.Family, defaultFont.Size * sizeFactor, FontStyle.Bold);
            segments.Add(new TextSegment { Text = headingText, Font = headingFont, Color = defaultColor, IsLineBreak = true });
        }
        
        /// <summary>
        /// Processes a code block's content
        /// </summary>
        private static void ProcessCodeBlockContent(string codeContent, Font defaultFont, List<TextSegment> segments)
        {
            var codeFont = new Font(FontFamilies.Monospace, defaultFont.Size);
            
            string[] codeLines = codeContent.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            for (int i = 0; i < codeLines.Length; i++)
            {
                segments.Add(new TextSegment { 
                    Text = codeLines[i], 
                    Font = codeFont, 
                    Color = Colors.Black,
                    BackgroundColor = Colors.LightGrey,
                    HasBackground = true,
                    IsLineBreak = true
                });
            }
        }
        
        /// <summary>
        /// Processes a list item
        /// </summary>
        private static void ProcessListItem(string itemText, int indentLevel, string marker, bool isOrdered, Font defaultFont, Color defaultColor, List<TextSegment> segments)
        {
            // Process the item text for inline formatting
            var itemSegments = new List<TextSegment>();
            ProcessInlineFormatting(itemText, defaultFont, defaultColor, itemSegments);
            
            // Set list properties for all segments
            foreach (var segment in itemSegments)
            {
                segment.IsList = true;
                segment.IsOrderedList = isOrdered;
                segment.ListIndent = indentLevel;
                segment.ListMarker = isOrdered ? marker + "." : marker;
            }
            
            // Add a line break at the end
            if (itemSegments.Count > 0 && !itemSegments[itemSegments.Count - 1].IsLineBreak)
            {
                itemSegments.Add(new TextSegment { 
                    Text = string.Empty, 
                    Font = defaultFont, 
                    Color = defaultColor, 
                    IsLineBreak = true,
                    IsList = true,
                    IsOrderedList = isOrdered,
                    ListIndent = indentLevel,
                    ListMarker = isOrdered ? marker + "." : marker
                });
            }
            
            segments.AddRange(itemSegments);
        }

        /// <summary>
        /// Finds inline formatting spans in text
        /// </summary>
        private static List<InlineFormatSpan> FindFormatSpans(string text)
        {
            var spans = new List<InlineFormatSpan>();
            
            // Find links [text](url)
            var linkRegex = new Regex(@"\[(.+?)\]\((.+?)\)");
            foreach (Match match in linkRegex.Matches(text))
            {
                spans.Add(new InlineFormatSpan {
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Text = match.Groups[1].Value,
                    IsLink = true,
                    Url = match.Groups[2].Value
                });
            }
            
            // Find bold+italic text (***text*** or ___text___)
            var boldItalicRegex = new Regex(@"(\*{3}|_{3})(.+?)(\*{3}|_{3})");
            foreach (Match match in boldItalicRegex.Matches(text))
            {
                // Skip if this is part of a link
                bool isPartOfLink = spans.Any(s => s.IsLink && 
                    ((match.Index >= s.StartIndex && match.Index < s.EndIndex) ||
                     (match.Index + match.Length > s.StartIndex && match.Index + match.Length <= s.EndIndex)));
                
                if (!isPartOfLink)
                {
                    spans.Add(new InlineFormatSpan {
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Text = match.Groups[2].Value,
                        IsBold = true,
                        IsItalic = true
                    });
                }
            }
            
            // Find bold text (**text** or __text__)
            var boldRegex = new Regex(@"(\*{2}|_{2})(.+?)(\*{2}|_{2})");
            foreach (Match match in boldRegex.Matches(text))
            {
                // Skip if this is part of a bold+italic match or link
                bool isPartOfOtherFormat = spans.Any(s => (s.IsBold && s.IsItalic) || s.IsLink && 
                    ((match.Index >= s.StartIndex && match.Index < s.EndIndex) ||
                     (match.Index + match.Length > s.StartIndex && match.Index + match.Length <= s.EndIndex)));
                
                if (!isPartOfOtherFormat)
                {
                    spans.Add(new InlineFormatSpan {
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Text = match.Groups[2].Value,
                        IsBold = true
                    });
                }
            }
            
            // Find italic text (*text* or _text_)
            var italicRegex = new Regex(@"(\*|_)(.+?)(\*|_)");
            foreach (Match match in italicRegex.Matches(text))
            {
                // Skip if this is part of a bold, bold+italic match, or link
                bool isPartOfOtherFormat = spans.Any(s => (s.IsBold || (s.IsBold && s.IsItalic) || s.IsLink) && 
                    ((match.Index >= s.StartIndex && match.Index < s.EndIndex) ||
                     (match.Index + match.Length > s.StartIndex && match.Index + match.Length <= s.EndIndex)));
                
                if (!isPartOfOtherFormat)
                {
                    spans.Add(new InlineFormatSpan {
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Text = match.Groups[2].Value,
                        IsItalic = true
                    });
                }
            }
            
            // Find inline code (`text`)
            var codeRegex = new Regex(@"`(.+?)`");
            foreach (Match match in codeRegex.Matches(text))
            {
                // Skip if this is part of a link
                bool isPartOfLink = spans.Any(s => s.IsLink && 
                    ((match.Index >= s.StartIndex && match.Index < s.EndIndex) ||
                     (match.Index + match.Length > s.StartIndex && match.Index + match.Length <= s.EndIndex)));
                
                if (!isPartOfLink)
                {
                    spans.Add(new InlineFormatSpan {
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Text = match.Groups[1].Value,
                        IsCode = true
                    });
                }
            }
            
            return spans;
        }

        /// <summary>
        /// Processes inline formatting in markdown
        /// </summary>
        private static void ProcessInlineFormatting(string line, Font defaultFont, Color defaultColor, List<TextSegment> segments)
        {
            // Find all formatting spans in the line
            var formatSpans = FindFormatSpans(line);
            
            // Process the line with formatting
            int currentIndex = 0;
            
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
                if (span.IsBold && span.IsItalic)
                {
                    spanFont = new Font(defaultFont.Family, defaultFont.Size, FontStyle.Bold | FontStyle.Italic);
                }
                else if (span.IsBold)
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
                
                var segment = new TextSegment { 
                    Text = span.Text, 
                    Font = spanFont, 
                    Color = span.IsCode ? Colors.Black : (span.IsLink ? Colors.Blue : defaultColor),
                    BackgroundColor = span.IsCode ? Colors.LightGrey : Colors.Transparent,
                    HasBackground = span.IsCode
                };
                
                // Add link information if needed
                if (span.IsLink)
                {
                    segment.IsLink = true;
                    segment.Url = span.Url;
                    segment.Text = segment.Text + " (" + span.Url + ")";
                }
                
                segments.Add(segment);
                
                currentIndex = span.EndIndex;
            }
            
            // Add any remaining text after the last format span
            if (currentIndex < line.Length)
            {
                string afterText = line.Substring(currentIndex);
                segments.Add(new TextSegment { Text = afterText, Font = defaultFont, Color = defaultColor });
            }
        }

        /// <summary>
        /// Calculates the layout of text segments
        /// </summary>
        public static void CalculateSegmentLayout(List<TextSegment> segments, int width, int padding, Graphics graphics)
        {
            float currentX = padding;
            float currentY = padding;
            float lineHeight = 0;
            float availableWidth = width - (padding * 2);
            
            // Group segments by line breaks
            var currentLine = new List<TextSegment>();
            
            foreach (var segment in segments)
            {
                if (segment.IsLineBreak)
                {
                    // Process the current line
                    LayoutLine(currentLine, currentX, currentY, availableWidth, graphics);
                    
                    // Calculate the max height of this line
                    lineHeight = currentLine.Count > 0 
                        ? currentLine.Max(s => graphics.MeasureString(s.Font, s.Text).Height) 
                        : 0;
                    
                    // Move to the next line
                    currentY += lineHeight > 0 ? lineHeight : 20;
                    currentX = padding;
                    currentLine.Clear();
                    
                    // Skip the rest for line break segments
                    segment.X = currentX;
                    segment.Y = currentY;
                    
                    // Add extra space for horizontal rules
                    if (segment.IsHorizontalRule)
                    {
                        currentY += 20; // Add space after horizontal rule
                    }
                    
                    continue;
                }
                
                // Handle list indentation
                if (segment.IsList)
                {
                    float indentSize = 20; // Base indent size
                    float markerWidth = 15; // Width for the marker (bullet or number)
                    float totalIndent = (segment.ListIndent * indentSize) + markerWidth + 5; // Add extra space between marker and text
                    
                    // If this is the first segment of a list item, adjust X position
                    if (currentLine.Count == 0)
                    {
                        currentX = padding + totalIndent;
                    }
                }
                
                // Handle blockquote indentation
                if (segment.IsBlockquote && currentLine.Count == 0)
                {
                    currentX = padding + 15; // Add extra indentation for blockquotes
                }
                
                // Add segment to current line
                segment.X = currentX;
                segment.Y = currentY;
                currentLine.Add(segment);
                
                // Move X position for next segment
                currentX += graphics.MeasureString(segment.Font, segment.Text).Width;
            }
            
            // Process the last line if not empty
            if (currentLine.Count > 0)
            {
                LayoutLine(currentLine, padding, currentY, availableWidth, graphics);
            }
        }

        /// <summary>
        /// Layouts a single line of text segments with word wrapping
        /// </summary>
        private static void LayoutLine(List<TextSegment> line, float startX, float startY, float availableWidth, Graphics graphics)
        {
            if (line.Count == 0)
                return;
                
            float currentX = startX;
            float currentY = startY;
            float lineHeight = line.Max(s => graphics.MeasureString(s.Font, s.Text).Height);
            
            // Check if the line needs wrapping
            float totalWidth = 0;
            foreach (var segment in line)
            {
                totalWidth += graphics.MeasureString(segment.Font, segment.Text).Width;
                if (totalWidth > availableWidth)
                    break;
            }
            
            if (totalWidth <= availableWidth)
            {
                // No wrapping needed, just update X positions
                foreach (var segment in line)
                {
                    segment.X = currentX;
                    segment.Y = currentY;
                    currentX += graphics.MeasureString(segment.Font, segment.Text).Width;
                }
                return;
            }
            
            // Need to wrap text
            List<TextSegment> newSegments = new List<TextSegment>();
            List<TextSegment> segmentsToRemove = new List<TextSegment>();
            
            foreach (var segment in line)
            {
                // Get the text and measure it
                string text = segment.Text;
                SizeF textSize = graphics.MeasureString(segment.Font, text);
                
                // If this segment fits on the current line, add it
                if (currentX + textSize.Width <= startX + availableWidth)
                {
                    segment.X = currentX;
                    segment.Y = currentY;
                    currentX += textSize.Width;
                    continue;
                }
                
                // This segment doesn't fit, try to wrap it
                segmentsToRemove.Add(segment);
                string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string currentWord = "";
                float wordX = currentX;
                
                foreach (string word in words)
                {
                    // Measure this word with a space
                    string wordWithSpace = currentWord.Length > 0 ? currentWord + " " + word : word;
                    SizeF wordSize = graphics.MeasureString(segment.Font, wordWithSpace);
                    
                    // If it fits on this line, add it to the current word
                    if (wordX + wordSize.Width <= startX + availableWidth)
                    {
                        currentWord = wordWithSpace;
                    }
                    else
                    {
                        // This word doesn't fit, create a new segment for the current word
                        if (currentWord.Length > 0)
                        {
                            var newSegment = new TextSegment
                            {
                                Text = currentWord,
                                Font = segment.Font,
                                Color = segment.Color,
                                X = wordX,
                                Y = currentY,
                                HasBackground = segment.HasBackground,
                                BackgroundColor = segment.BackgroundColor,
                                IsBlockquote = segment.IsBlockquote,
                                IsList = segment.IsList,
                                IsOrderedList = segment.IsOrderedList,
                                ListIndent = segment.ListIndent,
                                ListMarker = segment.ListMarker,
                                IsLink = segment.IsLink,
                                Url = segment.Url
                            };
                            
                            newSegments.Add(newSegment);
                        }
                        
                        // Move to the next line
                        currentY += lineHeight;
                        wordX = startX;
                        
                        // Handle list and blockquote indentation on wrapped lines
                        if (segment.IsList)
                        {
                            float indentSize = 20; // Base indent size
                            float markerWidth = 15; // Width for the marker (bullet or number)
                            float totalIndent = (segment.ListIndent * indentSize) + markerWidth + 5; // Add extra space between marker and text
                            wordX += totalIndent;
                        }
                        else if (segment.IsBlockquote)
                        {
                            wordX += 15; // Add extra indentation for blockquotes
                        }
                        
                        // Start with this word on the new line
                        currentWord = word;
                    }
                }
                
                // Add the last word if there is one
                if (currentWord.Length > 0)
                {
                    var lastSegment = new TextSegment
                    {
                        Text = currentWord,
                        Font = segment.Font,
                        Color = segment.Color,
                        X = wordX,
                        Y = currentY,
                        HasBackground = segment.HasBackground,
                        BackgroundColor = segment.BackgroundColor,
                        IsBlockquote = segment.IsBlockquote,
                        IsList = segment.IsList,
                        IsOrderedList = segment.IsOrderedList,
                        ListIndent = segment.ListIndent,
                        ListMarker = segment.ListMarker,
                        IsLink = segment.IsLink,
                        Url = segment.Url
                    };
                    
                    newSegments.Add(lastSegment);
                    
                    // Update currentX for the next segment
                    currentX = wordX + graphics.MeasureString(segment.Font, currentWord).Width;
                }
            }
            
            // Remove the original segments that were wrapped
            foreach (var segment in segmentsToRemove)
            {
                line.Remove(segment);
            }
            
            // Add all the new segments to the line
            line.AddRange(newSegments);
        }
    }
}
