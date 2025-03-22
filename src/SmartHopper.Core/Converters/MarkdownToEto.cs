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
using System.Text.RegularExpressions;
using Eto.Drawing;
using Markdig;
using Markdig.Syntax;

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
        /// <param name="font">The default font to use for unformatted text</param>
        /// <param name="textColor">The default color to use for unformatted text</param>
        /// <param name="graphics">The graphics context for measuring text</param>
        /// <returns>A list of formatted text segments</returns>
        public static List<TextSegment> ProcessMarkdown(string markdown, Font font, Color textColor, Graphics graphics)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return new List<TextSegment>();
            }
            
            var segments = new List<TextSegment>();
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var document = Markdig.Markdown.Parse(markdown, pipeline);
            
            // Process each block
            ProcessBlocks(document, segments, font, textColor, 0);
            
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
            // Use monospace font for code blocks
            var codeFont = new Font(FontFamilies.Monospace, defaultFont.Size);
            
            // Split code into lines
            string[] codeLines = codeContent.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            
            for (int i = 0; i < codeLines.Length; i++)
            {
                string line = codeLines[i];
                
                segments.Add(new TextSegment
                {
                    Text = line,
                    Font = codeFont,
                    Color = Colors.Black,
                    HasBackground = true,
                    BackgroundColor = Colors.LightGrey,
                    IsLineBreak = true
                });
            }
        }
        
        /// <summary>
        /// Processes a list item
        /// </summary>
        private static void ProcessListItem(string itemText, int indentLevel, string marker, bool isOrdered, Font defaultFont, Color defaultColor, List<TextSegment> segments)
        {
            // Create a segment for the list item
            var segment = new TextSegment
            {
                Text = itemText,
                Font = defaultFont,
                Color = defaultColor,
                IsList = true,
                IsOrderedList = isOrdered,
                ListIndent = indentLevel,
                ListMarker = isOrdered ? marker + "." : "•",
                IsLineBreak = true
            };
            
            segments.Add(segment);
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
        private static void ProcessInlineFormatting(string text, Font defaultFont, Color defaultColor, List<TextSegment> segments)
        {
            // Simple implementation - add the text as a single segment
            // In a full implementation, this would parse for bold, italic, links, etc.
            segments.Add(new TextSegment
            {
                Text = text,
                Font = defaultFont,
                Color = defaultColor,
                IsLineBreak = true
            });
        }

        /// <summary>
        /// Calculates the layout of text segments
        /// </summary>
        public static void CalculateSegmentLayout(List<TextSegment> segments, int width, int padding, Graphics graphics)
        {
            float currentX = padding;
            float currentY = padding;
            float availableWidth = width - (padding * 2);
            
            // Process each segment in sequence
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                
                // Handle line breaks
                if (segment.IsLineBreak)
                {
                    // Move to the next line
                    float lineHeight = 20; // Default line height
                    
                    // Add extra space for horizontal rules
                    if (segment.IsHorizontalRule)
                    {
                        lineHeight += 10;
                    }
                    
                    currentY += lineHeight;
                    currentX = padding;
                    segment.X = currentX;
                    segment.Y = currentY;
                    continue;
                }
                
                // Check if this segment is a heading (based on font size)
                bool isHeading = segment.Font != null && segment.Font.Size > 12;
                
                // If this is a heading and not at the start of the document, add some space before it
                if (isHeading && currentY > padding && (i == 0 || !segments[i-1].IsLineBreak))
                {
                    currentY += 15; // Add space before heading
                    currentX = padding; // Reset X position for heading
                }
                
                // Handle list indentation and markers
                if (segment.IsList)
                {
                    float indentSize = 20; // Base indent size
                    float listIndent = segment.ListIndent * indentSize;
                    
                    // Check if this is the first segment of a list item or a continuation
                    bool isFirstInListItem = i == 0 || segments[i - 1].IsLineBreak;
                    
                    if (isFirstInListItem)
                    {
                        // Reset X position at the start of a list item
                        currentX = padding + listIndent;
                        
                        // Add list marker as a separate segment if this is the first segment of a list item
                        if (!string.IsNullOrEmpty(segment.ListMarker))
                        {
                            var markerSegment = new TextSegment
                            {
                                Text = segment.ListMarker,
                                Font = segment.Font,
                                Color = segment.Color,
                                X = currentX - 20, // Position marker before the text
                                Y = currentY,
                                IsList = false // Prevent recursion
                            };
                            
                            // Insert the marker segment before the current one
                            segments.Insert(i, markerSegment);
                            i++; // Skip the newly inserted segment
                        }
                    }
                }
                
                // Handle blockquote indentation
                if (segment.IsBlockquote)
                {
                    // Add indentation for blockquotes if at the start of a line
                    if (currentX == padding)
                    {
                        currentX += 15;
                    }
                }
                
                // Position the segment
                segment.X = currentX;
                segment.Y = currentY;
                
                // For non-line-break segments, move to the next position
                if (!segment.IsLineBreak)
                {
                    // For headings, move to the next line after the heading
                    if (isHeading)
                    {
                        currentY += graphics.MeasureString(segment.Font, segment.Text).Height + 10;
                        currentX = padding;
                    }
                    else
                    {
                        // For regular text, just move to the right
                        // The actual wrapping will be handled by Eto.Forms DrawText with FormattedTextWrapMode.Word
                        currentX += graphics.MeasureString(segment.Font, segment.Text).Width;
                        
                        // If we've reached the end of the line, move to the next line
                        if (currentX > width - padding)
                        {
                            currentY += 20; // Standard line height
                            currentX = padding;
                        }
                    }
                }
            }
        }

        private static void ProcessBlocks(Markdig.Syntax.MarkdownDocument document, List<TextSegment> segments, Font font, Color textColor, int indentLevel)
        {
            foreach (var block in document)
            {
                if (block is Markdig.Syntax.HeadingBlock headingBlock)
                {
                    ProcessHeadingLine(new string('#', headingBlock.Level), headingBlock.Inline.ToString(), font, textColor, segments);
                }
                else if (block is Markdig.Syntax.ParagraphBlock paragraphBlock)
                {
                    ProcessInlineFormatting(paragraphBlock.Inline.ToString(), font, textColor, segments);
                }
                else if (block is Markdig.Syntax.ListBlock listBlock)
                {
                    foreach (var item in listBlock)
                    {
                        ProcessListItem(item.ToString(), indentLevel, "", false, font, textColor, segments);
                    }
                }
                else if (block is Markdig.Syntax.CodeBlock codeBlock)
                {
                    ProcessCodeBlockContent(codeBlock.ToString(), font, segments);
                }
                else if (block is Markdig.Syntax.QuoteBlock quoteBlock)
                {
                    foreach (var childBlock in quoteBlock)
                    {
                        var childDocument = new Markdig.Syntax.MarkdownDocument();
                        childDocument.Add(childBlock);
                        ProcessBlocks(childDocument, segments, font, textColor, indentLevel + 1);
                    }
                }
            }
        }
    }
}
