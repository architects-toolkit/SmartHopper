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

namespace SmartHopper.Core.Controls
{
    /// <summary>
    /// A custom control for displaying formatted text messages without borders or scrollbars.
    /// Supports markdown formatting and automatically adjusts its height based on content.
    /// </summary>
    public class FormattedMessageControl : Drawable
    {
        private string _text;
        private Font _font;
        private Color _textColor;
        private Color _backgroundColor;
        private int _padding;
        private List<MarkdownToEto.TextSegment> _segments;

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
                    Invalidate();
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
                    Invalidate();
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
                    Invalidate();
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
                    Invalidate();
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
                    ProcessText();
                    Invalidate();
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
            _segments = new List<MarkdownToEto.TextSegment>();

            // Enable mouse events for selection and context menu
            CanFocus = true;
            
            // Set minimum size
            MinimumSize = new Size(100, 20);
        }

        /// <summary>
        /// Processes text into segments and calculates layout
        /// </summary>
        private void ProcessText()
        {
            if (string.IsNullOrEmpty(_text))
            {
                _segments.Clear();
                Height = _padding * 2;
                return;
            }
            
            // Process markdown into segments
            using (var graphics = new Graphics(new Bitmap(1, 1, PixelFormat.Format32bppRgba)))
            {
                var segments = MarkdownToEto.ProcessMarkdown(_text, Width - (_padding * 2), _font, _textColor, graphics);
                
                // Calculate layout
                MarkdownToEto.CalculateSegmentLayout(segments, Width, _padding, graphics);
                
                // Calculate height based on segment positions
                // Group segments by their Y position to find the tallest segment in each line
                var lineGroups = segments.GroupBy(s => s.Y).OrderBy(g => g.Key);
                float totalHeight = _padding; // Start with top padding
                
                foreach (var lineGroup in lineGroups)
                {
                    float lineHeight = lineGroup.Max(s => graphics.MeasureString(s.Font, s.Text).Height);
                    totalHeight += lineHeight;
                }
                
                // Add bottom padding
                totalHeight += _padding;
                
                // Update control height
                Height = (int)Math.Ceiling(totalHeight);
                
                _segments = segments;
            }
        }

        /// <summary>
        /// Paints the control
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // Draw background
            e.Graphics.FillRectangle(_backgroundColor, new RectangleF(0, 0, Width, Height));
            
            // Draw text segments
            foreach (var segment in _segments)
            {
                // Draw segment background if needed
                if (segment.HasBackground)
                {
                    SizeF segmentSize = e.Graphics.MeasureString(segment.Font, segment.Text);
                    e.Graphics.FillRectangle(
                        segment.BackgroundColor, 
                        new RectangleF(segment.X - 2, segment.Y - 2, segmentSize.Width + 4, segmentSize.Height + 4)
                    );
                }
                
                // Draw blockquote marker if needed
                if (segment.IsBlockquote)
                {
                    SizeF segmentSize = e.Graphics.MeasureString(segment.Font, segment.Text);
                    // Draw background rectangle
                    e.Graphics.FillRectangle(
                        new Color(Colors.Blue, 0.3f), 
                        new RectangleF(segment.X - 10, segment.Y - 2, Width - segment.X, segmentSize.Height + 4)
                    );
                    
                    // Draw left border
                    e.Graphics.FillRectangle(
                        Colors.Gray, 
                        new RectangleF(segment.X - 10, segment.Y - 2, 3, segmentSize.Height + 4)
                    );
                }
                
                // Draw list marker if needed
                if (segment.IsList)
                {
                    float indentSize = 20; // Base indent size
                    float markerWidth = 15; // Width for the marker (bullet or number)
                    float markerX = segment.X - markerWidth - 5; // Position marker with 5px gap before text
                    float markerY = segment.Y;
                    
                    // Apply indentation based on list level
                    markerX -= (segment.ListIndent * indentSize);
                    
                    // Draw the marker
                    if (segment.IsOrderedList)
                    {
                        // Draw ordered list marker (number)
                        e.Graphics.DrawText(segment.Font, segment.Color, markerX, markerY, segment.ListMarker);
                    }
                    else
                    {
                        // Draw unordered list marker (bullet)
                        float bulletSize = segment.Font.Size / 2;
                        float bulletY = markerY + (segment.Font.Size / 2) - (bulletSize / 2);
                        
                        e.Graphics.FillEllipse(
                            segment.Color,
                            new RectangleF(markerX + 2, bulletY, bulletSize, bulletSize)
                        );
                    }
                }
                
                // Draw horizontal rule if needed
                if (segment.IsHorizontalRule)
                {
                    float ruleHeight = 2;
                    float ruleY = segment.Y + (segment.Font.Size / 2) - (ruleHeight / 2);
                    
                    e.Graphics.FillRectangle(
                        Colors.Gray,
                        new RectangleF(_padding, ruleY, Width - (_padding * 2), ruleHeight)
                    );
                    
                    // Skip drawing text for horizontal rules
                    continue;
                }
                
                // Draw the text
                e.Graphics.DrawText(segment.Font, segment.Color, segment.X, segment.Y, segment.Text);
            }
        }

        /// <summary>
        /// Called when the control is resized
        /// </summary>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ProcessText();
        }
    }
}
