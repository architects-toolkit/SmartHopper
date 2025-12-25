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
using Timer = System.Timers.Timer;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Custom attributes for AI components that displays the provider logo
    /// as a badge on the component.
    /// </summary>
    public class AIProviderComponentAttributes : GH_ComponentAttributes
    {
        private readonly AIProviderComponentBase owner;
        private const int BADGESIZE = 16; // Size of the provider logo badge
        private const float MINZOOMTHRESHOLD = 0.5f; // Minimum zoom level to show the badge
        protected const int PROVIDERSTRIPHEIGHT = 20; // Height of the provider strip

        // Hover state for inline provider label
        private RectangleF providerIconRect = RectangleF.Empty;
        private bool hoverProviderIcon;

        // Deferred label rendering support so derived classes can draw tooltips on top of their overlays
        private RectangleF? deferredLabelRect;
        private string deferredLabelText;

        // Timer-based auto-hide for inline label (disappears after 5s even if still hovered)
        // Purpose: avoid sticky labels when the cursor remains stationary.
        private Timer? providerLabelTimer;
        private bool providerLabelAutoHidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIProviderComponentAttributes"/> class.
        /// Creates a new instance of AIProviderComponentAttributes.
        /// </summary>
        /// <param name="owner">The AI component that owns these attributes.</param>
        public AIProviderComponentAttributes(AIProviderComponentBase owner)
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

            this.deferredLabelRect = null;
            this.deferredLabelText = null;

            // Only extend bounds if we have a valid provider
            if (!string.IsNullOrEmpty(this.owner.GetActualAIProviderName()))
            {
                var bounds = this.Bounds;
                bounds.Height += PROVIDERSTRIPHEIGHT;
                this.Bounds = bounds;
            }

            // Reset hover state on layout
            this.providerIconRect = RectangleF.Empty;
            this.hoverProviderIcon = false;
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
                this.providerIconRect = iconRect;

                // Draw the provider icon using GH methods (providerIcon guaranteed non-null here)
                GH_GraphicsUtil.RenderIcon(graphics, iconRect, providerIcon);

                // Draw inline label for provider when hovered and not auto-hidden (rendered after icon)
                if (this.hoverProviderIcon && !this.providerLabelAutoHidden && this.providerIconRect.Width > 0 && canvas.Viewport.Zoom >= MINZOOMTHRESHOLD)
                {
                    var label = $"Connected to {actualProviderName}";
                    if (this.ShouldDeferProviderLabelRendering())
                    {
                        this.deferredLabelRect = this.providerIconRect;
                        this.deferredLabelText = label;
                    }
                    else
                    {
                        InlineLabelRenderer.DrawInlineLabel(graphics, this.providerIconRect, label);
                        this.deferredLabelRect = null;
                        this.deferredLabelText = null;
                    }
                }
            }
        }

        /// <summary>
        /// Track mouse hover over the provider icon to trigger inline label rendering.
        /// </summary>
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            bool prev = this.hoverProviderIcon;

            if (sender?.Viewport.Zoom < MINZOOMTHRESHOLD)
            {
                this.hoverProviderIcon = false;
            }
            else
            {
                var pt = e.CanvasLocation;
                this.hoverProviderIcon = !this.providerIconRect.IsEmpty && this.providerIconRect.Contains(pt);
            }

            if (prev != this.hoverProviderIcon)
            {
                // Start/stop 5s auto-hide timer based on hover transitions
                if (this.hoverProviderIcon)
                {
                    this.providerLabelAutoHidden = false;
                    this.StartProviderLabelTimer();
                }
                else
                {
                    this.StopProviderLabelTimer();
                    this.providerLabelAutoHidden = false; // reset for next hover
                }

                this.owner.OnDisplayExpired(false);
            }

            return base.RespondToMouseMove(sender, e);
        }

        /// <summary>
        /// Starts a one-shot 5s timer to auto-hide the inline label and request a repaint.
        /// </summary>
        private void StartProviderLabelTimer()
        {
            this.StopProviderLabelTimer();
            this.providerLabelTimer = new Timer(5000) { AutoReset = false };
            this.providerLabelTimer.Elapsed += (_, __) =>
            {
                // Mark as auto-hidden and request display refresh
                this.providerLabelAutoHidden = true;
                try { this.owner?.OnDisplayExpired(false); } catch { /* ignore */ }
                this.StopProviderLabelTimer();
            };
            this.providerLabelTimer.Start();
        }

        /// <summary>
        /// Stops and disposes the provider label timer if active.
        /// </summary>
        private void StopProviderLabelTimer()
        {
            if (this.providerLabelTimer != null)
            {
                try { this.providerLabelTimer.Stop(); } catch { /* ignore */ }
                try { this.providerLabelTimer.Dispose(); } catch { /* ignore */ }
                this.providerLabelTimer = null;
            }
        }

        /// <summary>
        /// Derived classes can override to defer provider label rendering until after they finish custom drawing.
        /// </summary>
        /// <returns>True to delay tooltip rendering; otherwise false to draw immediately.</returns>
        protected virtual bool ShouldDeferProviderLabelRendering()
        {
            return false;
        }

        /// <summary>
        /// Draws the provider tooltip if rendering was deferred for foreground priority.
        /// </summary>
        /// <param name="graphics">Graphics context to draw on.</param>
        protected void RenderDeferredProviderLabel(Graphics graphics)
        {
            if (this.deferredLabelRect.HasValue && !string.IsNullOrEmpty(this.deferredLabelText))
            {
                InlineLabelRenderer.DrawInlineLabel(graphics, this.deferredLabelRect.Value, this.deferredLabelText);
                this.deferredLabelRect = null;
                this.deferredLabelText = null;
            }
        }
    }
}
