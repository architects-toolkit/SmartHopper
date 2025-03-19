/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Eto.Forms;
using Eto.Drawing;

namespace SmartHopper.Core.Converters
{
    /// <summary>
    /// Provides generic methods for converting between different text formats
    /// </summary>
    public static class FormatConverter
    {
        // This class can contain general format conversion utilities
        // that are not specific to any particular format
    }

    /// <summary>
    /// Provides methods for converting Markdown text to Eto.Forms controls
    /// </summary>
    public static class MarkdownToEtoConverter
    {
        /// <summary>
        /// Checks if the content likely contains Markdown formatting
        /// </summary>
        /// <param name="content">Text content to check</param>
        /// <returns>True if the content appears to contain Markdown</returns>
        public static bool ContainsMarkdown(string content)
        {
            if (string.IsNullOrEmpty(content))
                return false;
                
            // Check for common Markdown patterns
            return content.Contains("```") || // Code blocks
                   content.Contains("**") || // Bold
                   content.Contains("*") || // Italic
                   content.Contains("__") || // Bold
                   content.Contains("_") || // Italic
                   content.Contains("##") || // Headers
                   content.Contains("#") || // Headers
                   content.Contains("- ") || // Lists
                   content.Contains("1. ") || // Numbered lists
                   content.Contains("[") && content.Contains("]("); // Links
        }

        /// <summary>
        /// Converts Markdown text to formatted Eto.Forms controls
        /// </summary>
        /// <param name="markdown">Markdown text to convert</param>
        /// <returns>A control containing the formatted text</returns>
        public static Control ConvertToEtoControl(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return new Label { Text = string.Empty };
                
            try
            {
                // Parse the markdown document
                var document = Markdig.Markdown.Parse(markdown);
                
                // Create a container for all the formatted elements
                var container = new StackLayout
                {
                    Spacing = 5,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                
                // Process each block in the markdown document
                foreach (var block in document)
                {
                    ProcessMarkdownBlock(block, container);
                }
                
                return container;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting Markdown to formatted text: {ex.Message}");
                // Fallback to plain text on error
                return new Label { Text = markdown, Wrap = WrapMode.Word };
            }
        }
        
        /// <summary>
        /// Processes a Markdown block and adds appropriate controls to the container
        /// </summary>
        /// <param name="block">The Markdown block to process</param>
        /// <param name="container">The container to add controls to</param>
        private static void ProcessMarkdownBlock(Block block, StackLayout container)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    // Create a heading with appropriate font size
                    var headingLabel = new Label
                    {
                        Text = GetInlineText(heading.Inline),
                        Wrap = WrapMode.Word
                    };
                    
                    // Set font size based on heading level (h1-h6)
                    float fontSize = 14 - (heading.Level - 1);
                    headingLabel.Font = new Font(headingLabel.Font.Family, fontSize, FontStyle.Bold);
                    
                    container.Items.Add(headingLabel);
                    break;
                    
                case ParagraphBlock paragraph:
                    // Create a paragraph with normal text
                    var paragraphText = GetInlineText(paragraph.Inline);
                    if (!string.IsNullOrWhiteSpace(paragraphText))
                    {
                        var paragraphLabel = CreateFormattedLabel(paragraph.Inline);
                        container.Items.Add(paragraphLabel);
                    }
                    break;
                    
                case ListBlock list:
                    // Process list items
                    ProcessListBlock(list, container);
                    break;
                    
                case FencedCodeBlock codeBlock:
                    // Create a code block with monospace font and background
                    var codeText = string.Join(Environment.NewLine, codeBlock.Lines.Lines.Select(l => l.ToString()));
                    var codePanel = new Panel
                    {
                        Padding = new Padding(5),
                        BackgroundColor = Colors.LightGrey
                    };
                    
                    var codeLabel = new Label
                    {
                        Text = codeText,
                        Wrap = WrapMode.Word,
                        Font = new Font(FontFamilies.Monospace, 9)
                    };
                    
                    codePanel.Content = codeLabel;
                    container.Items.Add(codePanel);
                    break;
                    
                case QuoteBlock quoteBlock:
                    // Create a quote block with left border
                    var quotePanel = new Panel
                    {
                        Padding = new Padding(10, 5, 5, 5)
                    };
                    
                    // Add a left border to indicate a quote
                    quotePanel.Style = "border-left: 3px solid #ccc; padding-left: 10px;";
                    
                    var quoteContainer = new StackLayout
                    {
                        Spacing = 5,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch
                    };
                    
                    // Process each block in the quote
                    foreach (var innerBlock in quoteBlock)
                    {
                        ProcessMarkdownBlock(innerBlock, quoteContainer);
                    }
                    
                    quotePanel.Content = quoteContainer;
                    container.Items.Add(quotePanel);
                    break;
                    
                // Add more block types as needed
                
                default:
                    // For any other block types, just get the text
                    if (block is LeafBlock leafBlock && leafBlock.Inline != null)
                    {
                        var label = CreateFormattedLabel(leafBlock.Inline);
                        container.Items.Add(label);
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Processes a Markdown list block and adds appropriate controls to the container
        /// </summary>
        /// <param name="list">The list block to process</param>
        /// <param name="container">The container to add controls to</param>
        private static void ProcessListBlock(ListBlock list, StackLayout container)
        {
            int itemNumber = 1;
            
            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    // Create a container for the list item with bullet or number
                    var itemContainer = new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 5,
                        VerticalContentAlignment = VerticalAlignment.Top
                    };
                    
                    // Add bullet or number
                    string prefix = list.IsOrdered ? $"{itemNumber++}. " : "• ";
                    itemContainer.Items.Add(new Label
                    {
                        Text = prefix,
                        Width = 20,
                        VerticalAlignment = VerticalAlignment.Top
                    });
                    
                    // Container for the list item content
                    var contentContainer = new StackLayout
                    {
                        Spacing = 5,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch
                    };
                    
                    // Process each block in the list item
                    foreach (var block in listItem)
                    {
                        ProcessMarkdownBlock(block, contentContainer);
                    }
                    
                    itemContainer.Items.Add(contentContainer);
                    container.Items.Add(itemContainer);
                }
            }
        }
        
        /// <summary>
        /// Creates a formatted label from Markdown inline content
        /// </summary>
        /// <param name="inline">The inline content to format</param>
        /// <returns>A formatted label control</returns>
        private static Label CreateFormattedLabel(ContainerInline inline)
        {
            // Create a label with the text
            var label = new Label
            {
                Text = GetInlineText(inline),
                Wrap = WrapMode.Word
            };
            
            // Check for formatting in the inline content
            bool hasBold = false;
            bool hasItalic = false;
            bool hasUnderline = false;
            
            // Scan for formatting
            ScanForFormatting(inline, ref hasBold, ref hasItalic, ref hasUnderline);
            
            // Apply formatting if found
            FontStyle style = FontStyle.None;
            if (hasBold)
                style |= FontStyle.Bold;
            if (hasItalic)
                style |= FontStyle.Italic;
                
            if (style != FontStyle.None)
                label.Font = new Font(label.Font.Family, label.Font.Size, style);
                
            // Handle underline separately (if Eto.Forms supports it)
            if (hasUnderline)
            {
                // Note: If Eto.Forms doesn't directly support underline,
                // we could use TextDecoration or a custom approach
                // For now, we'll just indicate it in the text
                label.ToolTip = "This text contains underlined content";
            }
            
            return label;
        }
        
        /// <summary>
        /// Scans inline content for formatting (bold, italic, underline)
        /// </summary>
        /// <param name="inline">The inline content to scan</param>
        /// <param name="hasBold">Reference to a boolean indicating if bold formatting was found</param>
        /// <param name="hasItalic">Reference to a boolean indicating if italic formatting was found</param>
        /// <param name="hasUnderline">Reference to a boolean indicating if underline formatting was found</param>
        private static void ScanForFormatting(ContainerInline inline, ref bool hasBold, ref bool hasItalic, ref bool hasUnderline)
        {
            if (inline == null)
                return;
                
            foreach (var item in inline)
            {
                if (item is EmphasisInline emphasis)
                {
                    // Check for combined bold+italic (*** or ___)
                    if (emphasis.DelimiterCount >= 3)
                    {
                        hasBold = true;
                        hasItalic = true;
                    }
                    // Check for bold (** or __)
                    else if (emphasis.DelimiterCount == 2)
                    {
                        hasBold = true;
                    }
                    // Check for italic (* or _)
                    else if (emphasis.DelimiterCount == 1)
                    {
                        hasItalic = true;
                    }
                    
                    // Recursively scan the emphasis content
                    ScanForFormatting(emphasis, ref hasBold, ref hasItalic, ref hasUnderline);
                }
                else if (item is HtmlInline html)
                {
                    // Check for HTML tags like <u> for underline
                    string htmlTag = html.Tag.ToLowerInvariant();
                    if (htmlTag.Contains("<u>") || htmlTag.Contains("<u "))
                    {
                        hasUnderline = true;
                    }
                    // Could add more HTML tag handling here
                }
                else if (item is ContainerInline container)
                {
                    // Recursively scan other containers
                    ScanForFormatting(container, ref hasBold, ref hasItalic, ref hasUnderline);
                }
            }
        }
        
        /// <summary>
        /// Extracts text from Markdown inline content
        /// </summary>
        /// <param name="inline">The inline content to extract text from</param>
        /// <returns>Plain text representation of the inline content</returns>
        private static string GetInlineText(ContainerInline inline)
        {
            if (inline == null)
                return string.Empty;
                
            var text = string.Empty;
            
            foreach (var item in inline)
            {
                switch (item)
                {
                    case LiteralInline literal:
                        text += literal.Content.ToString();
                        break;
                        
                    case EmphasisInline emphasis:
                        // Just get the text without adding any formatting indicators
                        text += GetInlineText(emphasis);
                        break;
                        
                    case LinkInline link:
                        // For links, use the label text
                        text += GetInlineText(link);
                        if (!string.IsNullOrEmpty(link.Url))
                        {
                            text += $" ({link.Url})";
                        }
                        break;
                        
                    case CodeInline code:
                        // For inline code, just use the content
                        text += code.Content;
                        break;
                        
                    case LineBreakInline lb:
                        text += Environment.NewLine;
                        break;
                        
                    case HtmlInline html:
                        // For HTML content, extract the text between tags
                        if (html.Tag.StartsWith("<") && !html.Tag.StartsWith("</"))
                        {
                            // This is an opening tag, we don't add anything to the text
                        }
                        else if (html.Tag.StartsWith("</"))
                        {
                            // This is a closing tag, we don't add anything to the text
                        }
                        else
                        {
                            // This is text content within HTML tags
                            text += html.Tag;
                        }
                        break;
                        
                    default:
                        // For other inline types, try to get their content
                        if (item is ContainerInline container)
                        {
                            text += GetInlineText(container);
                        }
                        break;
                }
            }
            
            return text;
        }
        
        /// <summary>
        /// Converts Markdown text to plain text, removing all formatting
        /// </summary>
        /// <param name="markdown">Markdown text to convert</param>
        /// <returns>Plain text without any Markdown formatting</returns>
        public static string ConvertToPlainText(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;
                
            try
            {
                // Parse the markdown document
                var document = Markdig.Markdown.Parse(markdown);
                
                var plainText = new StringBuilder();
                
                // Process each block in the markdown document
                foreach (var block in document)
                {
                    ExtractPlainText(block, plainText);
                    
                    // Add a newline between blocks
                    if (plainText.Length > 0 && !plainText.ToString().EndsWith(Environment.NewLine))
                    {
                        plainText.AppendLine();
                    }
                }
                
                return plainText.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting Markdown to plain text: {ex.Message}");
                return markdown;
            }
        }
        
        /// <summary>
        /// Extracts plain text from a Markdown block
        /// </summary>
        /// <param name="block">The Markdown block to process</param>
        /// <param name="plainText">StringBuilder to append the plain text to</param>
        private static void ExtractPlainText(Block block, StringBuilder plainText)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    plainText.AppendLine(GetInlineText(heading.Inline));
                    break;
                    
                case ParagraphBlock paragraph:
                    plainText.AppendLine(GetInlineText(paragraph.Inline));
                    break;
                    
                case ListBlock list:
                    int itemNumber = 1;
                    foreach (var item in list)
                    {
                        if (item is ListItemBlock listItem)
                        {
                            string prefix = list.IsOrdered ? $"{itemNumber++}. " : "• ";
                            plainText.Append(prefix);
                            
                            foreach (var listItemBlock in listItem)
                            {
                                ExtractPlainText(listItemBlock, plainText);
                            }
                        }
                    }
                    break;
                    
                case FencedCodeBlock codeBlock:
                    var codeText = string.Join(Environment.NewLine, codeBlock.Lines.Lines.Select(l => l.ToString()));
                    plainText.AppendLine(codeText);
                    break;
                    
                case QuoteBlock quoteBlock:
                    foreach (var innerBlock in quoteBlock)
                    {
                        plainText.Append("> ");
                        ExtractPlainText(innerBlock, plainText);
                    }
                    break;
                    
                default:
                    if (block is LeafBlock leafBlock && leafBlock.Inline != null)
                    {
                        plainText.AppendLine(GetInlineText(leafBlock.Inline));
                    }
                    else if (block is ContainerBlock containerBlock)
                    {
                        foreach (var innerBlock in containerBlock)
                        {
                            ExtractPlainText(innerBlock, plainText);
                        }
                    }
                    break;
            }
        }
    }
}
