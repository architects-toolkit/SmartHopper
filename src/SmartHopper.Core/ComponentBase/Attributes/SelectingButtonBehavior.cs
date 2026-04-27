/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;
using Timer = System.Timers.Timer;

namespace SmartHopper.Core.ComponentBase.Attributes
{
    /// <summary>
    /// Encapsulates the mouse/hover/render state required to host a "Select" button
    /// on a component's attributes. The two public attributes classes
    /// (<c>SelectingComponentAttributes</c> and <c>AISelectingComponentAttributes</c>)
    /// differ only in their parent type and the placement of the button inside
    /// <see cref="GH_Attributes{T}.Layout"/>; by delegating state and event handling
    /// here they stop duplicating ~100 lines of identical logic.
    /// </summary>
    /// <remarks>
    /// This helper owns:
    /// <list type="bullet">
    ///   <item>button bounds, hover / click flags, auto-hide timer state,</item>
    ///   <item>the selected-objects bounds cache used by the dashed overlay,</item>
    ///   <item>event handlers that know how to update the owner's display.</item>
    /// </list>
    /// The hosting attributes class is responsible only for computing
    /// <see cref="ButtonBounds"/> in its own <c>Layout</c> pass and for forwarding
    /// the <c>Render</c> / mouse callbacks.
    /// </remarks>
    internal sealed class SelectingButtonBehavior
    {
        private readonly GH_Component owner;
        private readonly ISelectingComponent selectingComponent;

        private Rectangle buttonBounds;
        private bool isHovering;
        private bool isClicking;

        // Timer-based auto-hide of the visual highlight for selected objects.
        // Purpose: ensure the dashed highlight disappears after 5s even if the cursor stays hovered.
        private Timer? selectDisplayTimer;
        private bool selectAutoHidden;

        // Cached bounds for selected objects during hover session.
        // Computed once when hover starts, cleared when hover ends.
        private Dictionary<Guid, RectangleF>? cachedSelectedBounds;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectingButtonBehavior"/>
        /// class.
        /// </summary>
        /// <param name="owner">The component hosting the button; used for
        /// <see cref="GH_ActiveObject.ExpireSolution"/> and
        /// <see cref="IGH_ActiveObject.OnDisplayExpired"/> calls.</param>
        /// <param name="selectingComponent">The selection-bearing component whose
        /// <see cref="ISelectingComponent.EnableSelectionMode"/> is triggered by
        /// button clicks.</param>
        public SelectingButtonBehavior(GH_Component owner, ISelectingComponent selectingComponent)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.selectingComponent = selectingComponent ?? throw new ArgumentNullException(nameof(selectingComponent));
        }

        /// <summary>
        /// Gets or sets the rectangular area occupied by the button on the canvas.
        /// The hosting attributes class must assign this during its layout pass.
        /// </summary>
        public Rectangle ButtonBounds
        {
            get => this.buttonBounds;
            set => this.buttonBounds = value;
        }

        /// <summary>
        /// Draws the button itself in the <see cref="GH_CanvasChannel.Objects"/>
        /// channel. Callers should invoke this from their <c>Render</c> override.
        /// </summary>
        public void RenderButton(GH_Canvas canvas, Graphics graphics, bool selected, bool locked)
        {
            SelectingComponentCore.RenderSelectButton(
                canvas,
                graphics,
                this.buttonBounds,
                this.isHovering,
                this.isClicking,
                selected,
                locked);
        }

        /// <summary>
        /// Draws the dashed highlight around cached selected components in the
        /// <see cref="GH_CanvasChannel.Overlay"/> channel. Honors the hover-based
        /// auto-hide policy.
        /// </summary>
        public void RenderOverlay(GH_Canvas canvas, Graphics graphics)
        {
            SelectingComponentCore.RenderSelectionOverlay(
                canvas,
                graphics,
                this.buttonBounds,
                this.cachedSelectedBounds,
                this.selectAutoHidden);
        }

        /// <summary>
        /// Handles left-mouse-down inside the button bounds by starting a click,
        /// triggering selection mode and refreshing the bounds cache.
        /// </summary>
        /// <returns><see cref="GH_ObjectResponse.Handled"/> if the click was
        /// consumed, otherwise <see cref="GH_ObjectResponse.Ignore"/> so the
        /// caller can fall back to the base implementation.</returns>
        public GH_ObjectResponse OnMouseDown(GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && this.buttonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y))
            {
                this.isClicking = true;
                this.owner.ExpireSolution(true);
                this.selectingComponent.EnableSelectionMode();

                // Refresh cache after selection completes
                this.CacheSelectedBounds();
                return GH_ObjectResponse.Handled;
            }

            return GH_ObjectResponse.Ignore;
        }

        /// <summary>
        /// Tracks hover transitions over the button, manages the auto-hide timer
        /// and primes/clears the bounds cache so the overlay can render without
        /// repeatedly querying the document during a mouse move.
        /// </summary>
        public void OnMouseMove(GH_CanvasMouseEvent e)
        {
            var was = this.isHovering;
            this.isHovering = this.buttonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y);
            if (was == this.isHovering)
            {
                return;
            }

            if (this.isHovering)
            {
                this.selectAutoHidden = false;
                this.CacheSelectedBounds();
                this.StartSelectDisplayTimer();
            }
            else
            {
                this.StopSelectDisplayTimer();
                this.selectAutoHidden = false; // reset for next hover
                this.cachedSelectedBounds = null; // clear cache on hover end
            }

            // Hover-only visual changes: invalidate display, not solution
            this.owner.OnDisplayExpired(false);
        }

        /// <summary>
        /// Clears the click flag on mouse-up and expires the solution so the
        /// button repaints in its idle palette.
        /// </summary>
        /// <returns><see cref="GH_ObjectResponse.Ignore"/> always; the caller is
        /// expected to chain to the base implementation.</returns>
        public GH_ObjectResponse OnMouseUp(GH_CanvasMouseEvent e)
        {
            if (this.isClicking)
            {
                this.isClicking = false;
                this.owner.ExpireSolution(true);
            }

            return GH_ObjectResponse.Ignore;
        }

        /// <summary>
        /// Restarts the 5-second auto-hide timer for the selection highlight.
        /// </summary>
        private void StartSelectDisplayTimer()
        {
            SelectingComponentCore.RestartSelectDisplayTimer(
                ref this.selectDisplayTimer,
                () =>
                {
                    this.selectAutoHidden = true;
                    try { this.owner?.OnDisplayExpired(false); } catch { /* ignore */ }
                });
        }

        /// <summary>
        /// Stops and disposes the selection-display timer if active.
        /// </summary>
        private void StopSelectDisplayTimer()
        {
            SelectingComponentCore.StopSelectDisplayTimer(ref this.selectDisplayTimer);
        }

        /// <summary>
        /// Captures the current bounds of all selected objects. Called once when
        /// the hover starts so the overlay renderer works from fresh data without
        /// walking the document on every mouse-move.
        /// </summary>
        private void CacheSelectedBounds()
        {
            this.cachedSelectedBounds = SelectingComponentCore.BuildSelectedBounds(this.selectingComponent);
        }
    }
}
