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
using Eto.Forms;
using Eto.Drawing;
using SmartHopper.Core.Converters;
using Markdig;

namespace SmartHopper.Core.Controls
{
    /// <summary>
    /// A custom control for displaying formatted text messages without borders or scrollbars.
    /// Supports markdown formatting and automatically adjusts its height based on content.
    /// </summary>
    public class FormattedMessageControl : Panel
    {
        private string _text;
        private Font _font;
        private Color _textColor;
        private Color _backgroundColor;
        private int _padding;
        private StackLayout _contentLayout;

        /// <summary>
        /// Gets or sets the text content to display
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    ProcessText();
                }
            }
        }

        /// <summary>
        /// Gets or sets the font used to display the text
        /// </summary>
        public Font ControlFont
        {
            get => _font;
            set
            {
                if (_font != value)
                {
                    _font = value;
                    ProcessText();
                }
            }
        }

        /// <summary>
        /// Gets or sets the text color
        /// </summary>
        public Color TextColor
        {
            get => _textColor;
            set
            {
                if (_textColor != value)
                {
                    _textColor = value;
                    ProcessText();
                }
            }
        }

        /// <summary>
        /// Gets or sets the background color
        /// </summary>
        public Color ControlBackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor != value)
                {
                    _backgroundColor = value;
                    BackgroundColor = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the padding around the text
        /// </summary>
        public int ControlPadding
        {
            get => _padding;
            set
            {
                if (_padding != value)
                {
                    _padding = value;
                    _contentLayout.Padding = new Padding(_padding);
                    ProcessText();
                }
            }
        }

        /// <summary>
        /// Creates a new FormattedMessageControl
        /// </summary>
        public FormattedMessageControl()
        {
            _text = string.Empty;
            _font = SystemFonts.Default();
            _textColor = Colors.Black;
            _backgroundColor = Colors.Transparent;
            _padding = 10;

            // Create a stack layout for content
            _contentLayout = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 0,
                Padding = new Padding(_padding),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top
            };

            Content = _contentLayout;
            
            // Set control properties
            BackgroundColor = _backgroundColor;
            MinimumSize = new Size(100, 20);
        }

        /// <summary>
        /// Processes text into formatted controls and displays them in the layout
        /// </summary>
        private void ProcessText()
        {
            // Clear existing content
            _contentLayout.Items.Clear();

            if (string.IsNullOrEmpty(_text))
            {
                return;
            }

            // Create controls from markdown
            using (var graphics = new Graphics(new Bitmap(1, 1, PixelFormat.Format32bppRgba)))
            {
                var controls = CreateControlsFromMarkdown(_text, graphics);
                
                // Add controls to layout
                foreach (var control in controls)
                {
                    _contentLayout.Items.Add(new StackLayoutItem(control, true) {
                        VerticalAlignment = VerticalAlignment.Top,
                        Expand = false // Don't expand to fill available space
                    });
                }
            }
        }

        /// <summary>
        /// Creates Eto.Forms controls from markdown text
        /// </summary>
        private List<Control> CreateControlsFromMarkdown(string markdownText, Graphics graphics)
        {
            var result = new List<Control>();
            
            // Parse markdown
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var document = Markdig.Parsers.MarkdownParser.Parse(markdownText, pipeline);
            
            // Process each block in the document
            foreach (var block in document)
            {
                ProcessBlock(block, result, graphics);
            }
            
            return result;
        }

        /// <summary>
        /// Processes a markdown block and converts it to Eto.Forms controls
        /// </summary>
        private void ProcessBlock(Markdig.Syntax.Block block, List<Control> controls, Graphics graphics)
        {
            if (block is Markdig.Syntax.HeadingBlock headingBlock)
            {
                // Extract the text content
                string headingText = MarkdownToEto.ExtractInlineText(headingBlock.Inline);
                
                // Create a label with appropriate font size based on heading level
                float size = _font.Size;
                switch (headingBlock.Level)
                {
                    case 1: size = _font.Size * 2.0f; break;
                    case 2: size = _font.Size * 1.7f; break;
                    case 3: size = _font.Size * 1.4f; break;
                    case 4: size = _font.Size * 1.2f; break;
                    case 5: size = _font.Size * 1.0f; break;
                    case 6: size = _font.Size * 0.9f; break;
                }
                
                var headingLabel = new Label
                {
                    Text = headingText,
                    Font = new Font(_font.Family, size, _font.Bold ? FontStyle.Bold : FontStyle.None),
                    TextColor = _textColor,
                    Wrap = WrapMode.Word,
                    VerticalAlignment = VerticalAlignment.Top
                };
                
                controls.Add(headingLabel);
            }
            else if (block is Markdig.Syntax.ParagraphBlock paragraphBlock)
            {
                // Extract the text content with proper inline formatting
                string paragraphText = MarkdownToEto.ExtractInlineText(paragraphBlock.Inline);
                
                // Use a DynamicLayout to handle rich text formatting
                var richTextLayout = new DynamicLayout
                {
                    Padding = new Padding(0),
                    Spacing = new Size(0, 0)
                };
                
                // Get formatted segments from MarkdownToEto
                var segments = MarkdownToEto.ProcessMarkdown(paragraphText, _font, _textColor, graphics);
                
                // Create a TableLayout for inline formatting
                var tableLayout = new TableLayout();
                var row = new TableRow();
                
                foreach (var segment in segments)
                {
                    var label = new Label
                    {
                        Text = segment.Text,
                        Font = segment.Font,
                        TextColor = segment.Color,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    
                    // Handle links
                    if (segment.IsLink && !string.IsNullOrEmpty(segment.Url))
                    {
                        var linkButton = new LinkButton
                        {
                            Text = segment.Text,
                            Font = segment.Font,
                            TextColor = Colors.Blue
                            //VerticalAlignment = VerticalAlignment.Top
                        };
                        
                        // Store URL in Tag for click event
                        linkButton.Tag = segment.Url;
                        
                        // Handle link click
                        linkButton.Click += (sender, e) => 
                        {
                            if (sender is LinkButton lb && lb.Tag is string url)
                            {
                                try
                                {
                                    Application.Instance.Open(url);
                                }
                                catch (Exception ex)
                                {
                                    // Show error message if URL can't be opened
                                    MessageBox.Show($"Could not open link: {ex.Message}", "Error", MessageBoxType.Error);
                                }
                            }
                        };
                        
                        row.Cells.Add(linkButton);
                    }
                    else
                    {
                        row.Cells.Add(label);
                    }
                }
                
                tableLayout.Rows.Add(row);
                
                // Create a container panel for the rich text
                var richTextPanel = new Panel
                {
                    Content = tableLayout,
                    Padding = new Padding(0),
                };
                
                richTextLayout.Add(richTextPanel);
                
                controls.Add(richTextLayout);
            }
            else if (block is Markdig.Syntax.ListBlock listBlock)
            {
                // Create a stack panel for the list
                var listPanel = new StackLayout
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 5, // Consistent spacing between items
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Top // Ensure vertical alignment is consistent
                };
                
                // Get the starting index for ordered lists
                int index = 1; // Default to 1
                if (listBlock.IsOrdered)
                {
                    // Try to parse the OrderedStart value as an integer
                    if (int.TryParse(listBlock.OrderedStart.ToString(), out int parsedValue))
                    {
                        index = parsedValue;
                    }
                }
                
                foreach (var item in listBlock)
                {
                    if (item is Markdig.Syntax.ListItemBlock listItemBlock)
                    {
                        // Extract the text content
                        string itemText = MarkdownToEto.ExtractBlockText(listItemBlock);
                        
                        // Create layout for this list item
                        var itemLayout = new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            VerticalContentAlignment = VerticalAlignment.Top // Ensure vertical alignment is consistent
                        };
                        
                        // Create marker based on list type
                        string marker = listBlock.IsOrdered ? $"{index}." : "•";
                        var markerLabel = new Label
                        {
                            Text = marker,
                            Font = _font,
                            TextColor = _textColor,
                            Width = 20,
                            TextAlignment = TextAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Top,
                            Height = (int)_font.LineHeight
                        };
                        
                        // Add marker to layout
                        itemLayout.Items.Add(new StackLayoutItem(markerLabel, false) {
                            VerticalAlignment = VerticalAlignment.Top
                        });
                        
                        // Create rich text content using the same approach as paragraphs
                        var contentTable = new TableLayout();
                        var contentRow = new TableRow();
                        
                        // Get formatted segments from MarkdownToEto
                        var segments = MarkdownToEto.ProcessMarkdown(itemText, _font, _textColor, graphics);
                        
                        foreach (var segment in segments)
                        {
                            if (segment.IsLink && !string.IsNullOrEmpty(segment.Url))
                            {
                                var linkButton = new LinkButton
                                {
                                    Text = segment.Text,
                                    Font = segment.Font,
                                    TextColor = Colors.Blue
                                    //VerticalAlignment = VerticalAlignment.Top
                                };
                                
                                // Store URL in Tag for click event
                                linkButton.Tag = segment.Url;
                                
                                // Handle link click
                                linkButton.Click += (sender, e) => 
                                {
                                    if (sender is LinkButton lb && lb.Tag is string url)
                                    {
                                        try
                                        {
                                            Application.Instance.Open(url);
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show($"Could not open link: {ex.Message}", "Error", MessageBoxType.Error);
                                        }
                                    }
                                };
                                
                                contentRow.Cells.Add(linkButton);
                            }
                            else
                            {
                                // Regular text segment
                                var segmentLabel = new Label
                                {
                                    Text = segment.Text,
                                    Font = segment.Font,
                                    TextColor = segment.Color,
                                    VerticalAlignment = VerticalAlignment.Top
                                };
                                
                                contentRow.Cells.Add(segmentLabel);
                            }
                        }
                        
                        contentTable.Rows.Add(contentRow);
                        
                        // Add content to layout
                        itemLayout.Items.Add(new StackLayoutItem(contentTable, true) {
                            VerticalAlignment = VerticalAlignment.Top
                        });
                        
                        // Add item to list
                        listPanel.Items.Add(new StackLayoutItem(itemLayout, true) {
                            VerticalAlignment = VerticalAlignment.Top,
                            Expand = false // Don't expand to fill available space
                        });
                        
                        // Increment index for ordered lists
                        if (listBlock.IsOrdered)
                        {
                            index++;
                        }
                    }
                }
                
                // Add list to controls
                controls.Add(listPanel);
            }
            else if (block is Markdig.Syntax.FencedCodeBlock fencedCodeBlock || block is Markdig.Syntax.CodeBlock codeBlock)
            {
                // Extract code content
                string codeContent = block is Markdig.Syntax.FencedCodeBlock ? 
                    MarkdownToEto.ExtractCodeBlockContent((Markdig.Syntax.FencedCodeBlock)block) : 
                    MarkdownToEto.ExtractCodeBlockContent((Markdig.Syntax.CodeBlock)block);
                
                // Create a code panel
                var codePanel = new Panel
                {
                    BackgroundColor = Colors.LightGrey,
                    Padding = new Padding(5),
                    MinimumSize = new Size(0, 10)
                };
                
                // Create a label with monospace font
                var codeLabel = new Label
                {
                    Text = codeContent,
                    Font = new Font(FontFamilies.Monospace, _font.Size),
                    TextColor = _textColor,
                    Wrap = WrapMode.Word,
                    VerticalAlignment = VerticalAlignment.Top
                };
                
                codePanel.Content = codeLabel;
                controls.Add(codePanel);
                
                // Use a minimal spacing marker
                var spacer = new Panel { 
                    Size = new Size(0, 2),
                    MinimumSize = new Size(0, 2),
                    BackgroundColor = Colors.Red
                };
                controls.Add(spacer);
            }
            else if (block is Markdig.Syntax.QuoteBlock quoteBlock)
            {
                // Extract quote content
                string quoteText = MarkdownToEto.ExtractBlockText(quoteBlock);
                
                // Create quote panel with a border
                var quotePanel = new Panel
                {
                    Padding = new Padding(10, 0, 0, 0)
                };
                
                // Add a left border to the panel
                var borderDrawable = new Drawable
                {
                    Width = 3,
                    BackgroundColor = Colors.Gray
                };
                
                var quoteLayout = new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 7,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                
                var quoteLabel = new Label
                {
                    Text = quoteText,
                    Font = _font,
                    TextColor = _textColor,
                    Wrap = WrapMode.Word,
                    VerticalAlignment = VerticalAlignment.Top
                };
                
                quoteLayout.Items.Add(new StackLayoutItem(borderDrawable, false));
                quoteLayout.Items.Add(new StackLayoutItem(quoteLabel, true));
                
                quotePanel.Content = quoteLayout;
                controls.Add(quotePanel);
            }
            else if (block is Markdig.Syntax.ThematicBreakBlock)
            {
                // Create a horizontal rule
                var hrPanel = new Panel
                {
                    Height = 20
                };
                
                var hrDrawable = new Drawable();
                hrDrawable.Paint += (sender, e) =>
                {
                    e.Graphics.DrawLine(Colors.Gray, 
                        new PointF(0, e.ClipRectangle.Height / 2), 
                        new PointF(e.ClipRectangle.Width, e.ClipRectangle.Height / 2));
                };
                
                hrPanel.Content = hrDrawable;
                controls.Add(hrPanel);
            }
        }
    }
}
