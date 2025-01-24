/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Drawing;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using SmartHopper.Config.Providers;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Custom attributes for AI components that displays the provider logo
    /// as a badge on the component.
    /// </summary>
    public class AIComponentAttributes : GH_ComponentAttributes
    {
        private readonly AIStatefulAsyncComponentBase _owner;
        private const int BADGE_SIZE = 16; // Size of the provider logo badge
        private const int BADGE_PADDING = 4; // Padding from component edge

        /// <summary>
        /// Creates a new instance of AIComponentAttributes
        /// </summary>
        /// <param name="owner">The AI component that owns these attributes</param>
        public AIComponentAttributes(AIStatefulAsyncComponentBase owner) : base(owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Renders the component with an additional provider logo badge
        /// </summary>
        /// <param name="canvas">The canvas being rendered to</param>
        /// <param name="graphics">The graphics object to use for drawing</param>
        /// <param name="channel">The current render channel</param>
        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if (channel == GH_CanvasChannel.Objects)
            {
                // Only render the badge if we have a valid provider
                if (string.IsNullOrEmpty(_owner._aiProvider))
                    return;

                // Get the provider icon
                var providerIcon = AIProviderRegistry.GetProviderIcon(_owner._aiProvider);
                if (providerIcon == null)
                    return;

                // Calculate badge position (top-right corner)
                var bounds = Bounds;

                // Calculate badge position (bottom right corner)
                var badgeRect = new RectangleF(
                    bounds.Left + bounds.Width / 2 - BADGE_SIZE / 2 - BADGE_PADDING - 2,
                    bounds.Top + bounds.Height / 2 + BADGE_SIZE,
                    BADGE_SIZE + BADGE_PADDING * 2,
                    BADGE_SIZE + BADGE_PADDING * 2);

                var iconRect = new RectangleF(
                    bounds.Left + bounds.Width / 2 - BADGE_SIZE / 2 - BADGE_PADDING - 2 + BADGE_PADDING,
                    bounds.Top + bounds.Height / 2 + BADGE_SIZE + BADGE_PADDING,
                    BADGE_SIZE,
                    BADGE_SIZE);

                // Draw the badge with a modern, semi-transparent background for visibility
                using (var brush = new SolidBrush(Color.FromArgb(200, Color.White)))
                {
                    graphics.FillEllipse(brush, badgeRect);
                }

                // Draw a border around the badge for a cleaner look
                using (var pen = new Pen(Color.FromArgb(50, Color.Black), 1f))
                {
                    graphics.DrawEllipse(pen, badgeRect);
                }

                // Draw the provider icon
                graphics.DrawImage(providerIcon, iconRect);
            }
        }

        /// <summary>
        /// Layout the component with additional space for the badge
        /// </summary>
        protected override void Layout()
        {
            base.Layout();

            ////// Expand the bounds slightly to accommodate the badge
            //var bounds = Bounds;
            //bounds.Width += BADGE_SIZE + (BADGE_PADDING * 2);
            //Bounds = bounds;
        }
    }
}
