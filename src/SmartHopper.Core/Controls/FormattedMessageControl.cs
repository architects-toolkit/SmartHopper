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
        private readonly List<MarkdownToEto.TextSegment> _segments = new List<MarkdownToEto.TextSegment>();

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

            // Enable mouse events for selection and context menu
            CanFocus = true;
            
            // Set minimum size
            MinimumSize = new Size(100, 20);
        }

        /// <summary>
        /// Processes the text and creates formatted segments
        /// </summary>
        private void ProcessText()
        {
            _segments.Clear();
            
            if (string.IsNullOrEmpty(_text))
                return;

            // Get the available width for text
            int availableWidth = Width - (_padding * 2);
            if (availableWidth <= 0)
                return;

            using (var graphics = new Graphics(new Bitmap(1, 1, PixelFormat.Format32bppRgba)))
            {
                // Always process as markdown
                _segments.AddRange(MarkdownToEto.ProcessMarkdown(_text, availableWidth, _font, _textColor, graphics));

                // Calculate the layout of segments
                MarkdownToEto.CalculateSegmentLayout(_segments, Width, _padding, graphics);
                
                // Calculate the required height based on the segments
                float totalHeight = 0;
                
                // Group segments by their Y position to properly calculate line heights
                var lineGroups = _segments.GroupBy(s => s.Y).OrderBy(g => g.Key);
                
                foreach (var lineGroup in lineGroups)
                {
                    float lineHeight = 0;
                    foreach (var segment in lineGroup)
                    {
                        SizeF segmentSize = graphics.MeasureString(segment.Font, segment.Text);
                        lineHeight = Math.Max(lineHeight, segmentSize.Height);
                    }
                    totalHeight += lineHeight;
                }
                
                // Set the height of the control to fit the content
                Height = (int)Math.Ceiling(totalHeight + (_padding * 2));
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
                    e.Graphics.FillRectangle(
                        Colors.Gray, 
                        new RectangleF(segment.X - _padding, segment.Y, 3, e.Graphics.MeasureString(segment.Font, segment.Text).Height)
                    );
                }
                
                // Draw list marker if needed
                if (segment.IsList)
                {
                    float indentSize = 20; // Base indent size
                    float markerX = segment.X - indentSize;
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
                        float bulletSize = segment.Font.Size / 3;
                        float bulletY = markerY + (segment.Font.Size / 2) - (bulletSize / 2);
                        
                        e.Graphics.FillEllipse(
                            segment.Color,
                            new RectangleF(markerX + 2, bulletY, bulletSize, bulletSize)
                        );
                    }
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
