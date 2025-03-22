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
                var segments = MarkdownToEto.ProcessMarkdown(_text, _font, _textColor, graphics);
                
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
                    // Draw a vertical line to the left of blockquote text
                    e.Graphics.FillRectangle(
                        Colors.Gray,
                        new RectangleF(segment.X - 10, segment.Y, 3, e.Graphics.MeasureString(segment.Font, segment.Text).Height)
                    );
                }
                
                // Draw horizontal rule if needed
                if (segment.IsHorizontalRule)
                {
                    e.Graphics.DrawLine(
                        Colors.Gray,
                        new PointF(_padding, segment.Y + 5),
                        new PointF(Width - _padding, segment.Y + 5)
                    );
                    continue; // Skip text drawing for horizontal rules
                }
                
                // Draw the text with wrapping
                if (!string.IsNullOrEmpty(segment.Text))
                {
                    // Use Eto.Forms' native text wrapping by setting a maximum width
                    RectangleF textRect = new RectangleF(
                        segment.X, 
                        segment.Y, 
                        Width - segment.X - _padding, // Available width from segment position to right edge
                        float.MaxValue // No height constraint
                    );
                    
                    // Draw the text with wrapping
                    e.Graphics.DrawText(segment.Font, new SolidBrush(segment.Color), textRect, segment.Text);
                }
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
