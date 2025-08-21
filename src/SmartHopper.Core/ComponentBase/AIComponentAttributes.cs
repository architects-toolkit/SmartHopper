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
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Custom attributes for AI components that displays the provider logo
    /// as a badge on the component.
    /// </summary>
    public class AIComponentAttributes : GH_ComponentAttributes
    {
        private readonly AIProviderComponentBase owner;
        private const int BADGESIZE = 16; // Size of the provider logo badge
        private const float MINZOOMTHRESHOLD = 0.5f; // Minimum zoom level to show the badge
        private const int PROVIDERSTRIPHEIGHT = 20; // Height of the provider strip

        /// <summary>
        /// Initializes a new instance of the <see cref="AIComponentAttributes"/> class.
        /// Creates a new instance of AIComponentAttributes.
        /// </summary>
        /// <param name="owner">The AI component that owns these attributes.</param>
        public AIComponentAttributes(AIProviderComponentBase owner)
            : base(owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Layout the component with additional space for the provider strip.
        /// </summary>
        protected override void Layout()
        {
            base.Layout();

            // Only extend bounds if we have a valid provider
            if (!string.IsNullOrEmpty(this.owner.GetActualAIProviderName()))
            {
                var bounds = this.Bounds;
                bounds.Height += PROVIDERSTRIPHEIGHT;
                this.Bounds = bounds;
            }
        }

        /// <summary>
        /// Renders the component with an additional provider strip.
        /// </summary>
        /// <param name="canvas">The canvas being rendered to.</param>
        /// <param name="graphics">The graphics object to use for drawing.</param>
        /// <param name="channel">The current render channel.</param>
        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if (channel == GH_CanvasChannel.Objects)
            {
                // Only render the provider strip if we have a valid provider and we're zoomed in enough
                if (string.IsNullOrEmpty(this.owner.GetActualAIProviderName()) || canvas.Viewport.Zoom < MINZOOMTHRESHOLD)
                {
                    return;
                }

                // Get the actual provider name (resolving Default to the actual provider)
                string actualProviderName = this.owner.GetActualAIProviderName();
                if (this.owner.GetActualAIProviderName() == AIProviderComponentBase.DEFAULT_PROVIDER)
                {
                    actualProviderName = SmartHopperSettings.Instance.DefaultAIProvider;
                }

                // Get the provider icon
                var providerIcon = ProviderManager.Instance.GetProvider(actualProviderName)?.Icon;
                if (providerIcon == null)
                {
                    return;
                }

                // Get the bounds of the component
                var bounds = this.Bounds;

                // Calculate strip position at the bottom of the component
                var stripRect = new RectangleF(
                    bounds.Left,
                    bounds.Bottom - PROVIDERSTRIPHEIGHT,
                    bounds.Width,
                    PROVIDERSTRIPHEIGHT);

                // Calculate starting X position to center the pack
                var startX = bounds.Left + ((bounds.Width - BADGESIZE) / 2);

                // Calculate icon position within strip
                var iconRect = new RectangleF(
                    startX,
                    bounds.Bottom - PROVIDERSTRIPHEIGHT + ((PROVIDERSTRIPHEIGHT - BADGESIZE) / 2),
                    BADGESIZE,
                    BADGESIZE);

                // Draw the provider icon using GH methods
                if (providerIcon != null)
                {
                    GH_GraphicsUtil.RenderIcon(graphics, iconRect, providerIcon);
                }
            }
        }
    }
}
