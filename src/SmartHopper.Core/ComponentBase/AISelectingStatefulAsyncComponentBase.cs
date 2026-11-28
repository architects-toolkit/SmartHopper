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

using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase.Attributes;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Mixins;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Combines <see cref="AIStatefulAsyncComponentBase"/> with the
    /// "Select Components" button flow. Selection plumbing is delegated to a
    /// composed <see cref="SelectingSupport"/>; this shell only wires lifecycle
    /// overrides.
    /// </summary>
    public abstract class AISelectingStatefulAsyncComponentBase : AIStatefulAsyncComponentBase, ISelectingComponent
    {
        private readonly SelectingSupport selection;

        /// <summary>
        /// Initializes a new instance of the <see cref="AISelectingStatefulAsyncComponentBase"/> class.
        /// </summary>
        protected AISelectingStatefulAsyncComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            this.selection = new SelectingSupport(this, this);
        }

        /// <summary>
        /// Selecting components mutate canvas state or consume selected objects —
        /// their work has side effects, so per-unit results are never reused.
        /// </summary>
        protected override bool SupportsResultReuse => false;

        /// <inheritdoc/>
        public List<IGH_DocumentObject> SelectedObjects => this.selection.SelectedObjects;

        /// <inheritdoc/>
        public void EnableSelectionMode() => this.selection.EnableSelectionMode();

        /// <inheritdoc/>
        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            this.selection.OnRemovedFromDocument();
        }

        /// <inheritdoc/>
        public override void CreateAttributes()
        {
            this.m_attributes = new AISelectingComponentAttributes(this, this);
        }

        /// <inheritdoc/>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => this.EnableSelectionMode());
        }

        /// <summary>
        /// Injects the synthetic <see cref="WellKnownInputs.Selection"/> entry when the
        /// canvas selection differs from the one the last run consumed, so a selection
        /// change retriggers processing exactly like a parameter change.
        /// </summary>
        protected override List<string> InputsChanged()
        {
            List<string> changedInputs = base.InputsChanged();
            if (this.selection.HasPendingSelectionChange)
            {
                changedInputs.Add(WellKnownInputs.Selection);
            }

            return changedInputs;
        }

        /// <summary>
        /// Captures the selection this run is about to consume, alongside the
        /// input-hash snapshot in the base implementation.
        /// </summary>
        protected override void OnEnteringProcessing()
        {
            base.OnEnteringProcessing();
            this.selection.CaptureRunSelection();
        }

        /// <summary>
        /// Commits the run-time selection after a successful run so a selection
        /// change made while work was in flight stays pending for the next solve.
        /// </summary>
        protected override void OnWorkerCompleted()
        {
            base.OnWorkerCompleted();
            this.selection.CommitRunSelection();
        }

        /// <summary>
        /// Commits the run-time selection on cancellation — a change made mid-run
        /// was never consumed and must stay pending.
        /// </summary>
        protected override void OnTasksCancelDetected()
        {
            base.OnTasksCancelDetected();
            this.selection.CommitRunSelection();
        }

        /// <summary>
        /// Commits the run-time selection on error for the same reason as
        /// <see cref="OnTasksCancelDetected"/>.
        /// </summary>
        protected override void OnEnteringError()
        {
            base.OnEnteringError();
            this.selection.CommitRunSelection();
        }

        /// <inheritdoc/>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            return base.Write(writer) && this.selection.Write(writer);
        }

        /// <inheritdoc/>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            return base.Read(reader) && this.selection.Read(reader);
        }
    }

    /// <summary>
    /// Attributes class for AI components that also expose a selection button.
    /// Inherits provider-badge rendering from <see cref="ComponentBadgesAttributes"/>
    /// and delegates the selection-button state and events to
    /// <see cref="SelectingButtonBehavior"/>, sharing that logic with
    /// <c>SelectingComponentAttributes</c>.
    /// </summary>
    public class AISelectingComponentAttributes : ComponentBadgesAttributes
    {
        private readonly AIProviderComponentBase owner;
        private readonly SelectingButtonBehavior behavior;

        /// <summary>
        /// Initializes a new instance of the <see cref="AISelectingComponentAttributes"/> class.
        /// </summary>
        /// <param name="owner">The AI provider component that owns these attributes.</param>
        /// <param name="selectingComponent">The selection-bearing contract
        /// implemented by the owner.</param>
        public AISelectingComponentAttributes(AIProviderComponentBase owner, ISelectingComponent selectingComponent)
            : base(owner)
        {
            this.owner = owner;
            this.behavior = new SelectingButtonBehavior(owner, selectingComponent);
        }

        /// <inheritdoc/>
        protected override void Layout()
        {
            base.Layout();

            const int margin = 5;
            const int buttonHeight = 24;
            var extraHeight = buttonHeight + margin;

            var bounds = this.Bounds;
            bounds.Height += extraHeight;
            this.Bounds = bounds;

            var providerTop = this.Bounds.Bottom - PROVIDERSTRIPHEIGHT;
            var width = (int)this.Bounds.Width - (2 * margin);
            var x = (int)this.Bounds.X + margin;
            var y = (int)(providerTop - margin - buttonHeight);
            this.behavior.ButtonBounds = new Rectangle(x, y, width, buttonHeight);
        }

        /// <inheritdoc/>
        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel == GH_CanvasChannel.Objects)
            {
                this.behavior.RenderButton(canvas, graphics, this.Selected, this.owner.Locked);
            }
            else if (channel == GH_CanvasChannel.Overlay)
            {
                this.behavior.RenderOverlay(canvas, graphics);

                // Draw the provider tooltip last so it stays above the button and selection overlays.
                this.RenderDeferredProviderLabel(graphics);
            }
        }

        /// <inheritdoc/>
        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var response = this.behavior.OnMouseDown(e);
            return response == GH_ObjectResponse.Handled ? response : base.RespondToMouseDown(sender, e);
        }

        /// <inheritdoc/>
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            this.behavior.OnMouseMove(e);
            return base.RespondToMouseMove(sender, e);
        }

        /// <inheritdoc/>
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            this.behavior.OnMouseUp(e);
            return base.RespondToMouseUp(sender, e);
        }

        /// <inheritdoc/>
        protected override bool ShouldDeferProviderLabelRendering()
        {
            // Defer tooltip rendering so it draws over the Select button UI elements.
            return true;
        }
    }
}
