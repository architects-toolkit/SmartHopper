/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * TestBadgesOneComponent: Grasshopper test component to visually verify the
 * inline badge rendering. Displays a single sample badge above the component
 * using the shared ComponentBadgesAttributes with an override that contributes
 * one additional badge and hover label.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Test.Badges
{
    /// <summary>
    /// Test component that renders exactly one badge via custom attributes.
    /// </summary>
    public class TestBadgesOneComponent : AIStatefulAsyncComponentBase
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("2B5E5F5A-6F4D-4C0F-8D99-0E9A04BB33A1");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.quinary;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestBadgesOneComponent"/> class.
        /// </summary>
        public TestBadgesOneComponent()
            : base("Test Badges: One", "TBadges1",
                   "Renders a single sample badge above the component for visual verification.",
                   "SmartHopper", "Testing")
        {
        }

        /// <inheritdoc />
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // No custom outputs; base adds Metrics
            base.RegisterOutputParams(pManager);
        }

        /// <inheritdoc />
        public override void CreateAttributes()
        {
            this.m_attributes = new OneBadgeAttributes(this);
        }

        /// <summary>
        /// No-op: badge test component defines no additional inputs.
        /// </summary>
        /// <param name="pManager">Input param manager.</param>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            // Intentionally empty
        }

        /// <summary>
        /// No-op: badge test component defines no additional outputs.
        /// </summary>
        /// <param name="pManager">Output param manager.</param>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            // Intentionally empty
        }

        /// <summary>
        /// Creates a minimal no-op worker since the component exists only to test inline badge rendering.
        /// </summary>
        /// <param name="progressReporter">Unused progress reporter.</param>
        /// <returns>A worker that performs no computation.</returns>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new NoopWorker(this, AddRuntimeMessage);
        }

        /// <summary>
        /// Minimal worker for badge test components. It gathers no input, performs no work,
        /// and sets only a lightweight status message so the async pipeline completes.
        /// </summary>
        private sealed class NoopWorker : AsyncWorkerBase
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="NoopWorker"/> class.
            /// </summary>
            /// <param name="parent">Component instance.</param>
            /// <param name="addRuntimeMessage">Delegate to add runtime messages.</param>
            public NoopWorker(GH_Component parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
            }

            /// <inheritdoc />
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 0;
            }

            /// <inheritdoc />
            public override Task DoWorkAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }

            /// <inheritdoc />
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = "Ready";
            }
        }

        /// <summary>
        /// Custom attributes contributing one additional badge.
        /// </summary>
        private class OneBadgeAttributes : ComponentBadgesAttributes
        {
            private static void DrawSampleBadge(Graphics g, float x, float y)
            {
                var size = 16f;
                using (var bg = new SolidBrush(Color.FromArgb(52, 152, 219))) // blue
                using (var pen = new Pen(Color.White, 1.5f))
                {
                    var rect = new RectangleF(x, y, size, size);
                    g.FillEllipse(bg, rect);
                    g.DrawEllipse(pen, rect);

                    // white dot
                    g.FillEllipse(Brushes.White, rect.X + size * 0.45f, rect.Y + size * 0.45f, size * 0.1f, size * 0.1f);
                }
            }

            public OneBadgeAttributes(AIProviderComponentBase owner) : base(owner) { }

            /// <inheritdoc />
            protected override IEnumerable<(Action<Graphics, float, float> draw, string label)> GetAdditionalBadges()
            {
                yield return (DrawSampleBadge, "Sample badge 1");
            }
        }
    }
}
