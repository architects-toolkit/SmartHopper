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
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel.Attributes;
using Grasshopper.GUI.Canvas.Interaction;
using SmartHopper.Config.Configuration;

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
        private const float MIN_ZOOM_THRESHOLD = 0.5f; // Minimum zoom level to show the badge
        private const int PROVIDER_STRIP_HEIGHT = 20; // Height of the provider strip

        /// <summary>
        /// Creates a new instance of AIComponentAttributes
        /// </summary>
        /// <param name="owner">The AI component that owns these attributes</param>
        public AIComponentAttributes(AIStatefulAsyncComponentBase owner) : base(owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Layout the component with additional space for the provider strip
        /// </summary>
        protected override void Layout()
        {
            base.Layout();

            // Only extend bounds if we have a valid provider
            if (!string.IsNullOrEmpty(_owner._aiProvider))
            {
                var bounds = Bounds;
                bounds.Height += PROVIDER_STRIP_HEIGHT;
                Bounds = bounds;
            }
        }

        /// <summary>
        /// Renders the component with an additional provider strip
        /// </summary>
        /// <param name="canvas">The canvas being rendered to</param>
        /// <param name="graphics">The graphics object to use for drawing</param>
        /// <param name="channel">The current render channel</param>
        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if (channel == GH_CanvasChannel.Objects)
            {
                // Only render the provider strip if we have a valid provider and we're zoomed in enough
                if (string.IsNullOrEmpty(_owner._aiProvider) || canvas.Viewport.Zoom < MIN_ZOOM_THRESHOLD)
                    return;

                // Get the provider icon
                var providerIcon = SmartHopperSettings.GetProviderIcon(_owner._aiProvider);
                if (providerIcon == null)
                    return;

                // Get the bounds of the component
                var bounds = Bounds;

                // Calculate strip position at the bottom of the component
                var stripRect = new RectangleF(
                    bounds.Left,
                    bounds.Bottom - PROVIDER_STRIP_HEIGHT,
                    bounds.Width,
                    PROVIDER_STRIP_HEIGHT);

                // Calculate starting X position to center the pack
                var startX = bounds.Left + (bounds.Width - BADGE_SIZE) / 2;

                // Calculate icon position within strip
                var iconRect = new RectangleF(
                    startX,
                    bounds.Bottom - PROVIDER_STRIP_HEIGHT + (PROVIDER_STRIP_HEIGHT - BADGE_SIZE) / 2,
                    BADGE_SIZE,
                    BADGE_SIZE);

                // Draw the provider icon using GH methods
                if (providerIcon != null)
                {
                    GH_GraphicsUtil.RenderIcon(graphics, iconRect, providerIcon);
                }
            }
        }
    }
}
