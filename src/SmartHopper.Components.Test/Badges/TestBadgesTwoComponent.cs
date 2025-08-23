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
 * TestBadgesTwoComponent: Verifies two inline badges rendering and hover labels.
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
    /// Test component that renders two sample badges via custom attributes.
    /// </summary>
    public class TestBadgesTwoComponent : AIStatefulAsyncComponentBase
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("A1A8E7D1-5F40-4F8C-8D8C-2E7DE55F6B22");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public TestBadgesTwoComponent()
            : base("Test Badges: Two", "TBadges2",
                   "Renders two sample badges above the component for visual verification.",
                   "SmartHopper", "Testing")
        {
        }

        public override void CreateAttributes()
        {
            this.m_attributes = new TwoBadgesAttributes(this);
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

        private class TwoBadgesAttributes : ComponentBadgesAttributes
        {
            private const float S = 16f;

            private static void DrawBlueBadge(Graphics g, float x, float y)
            {
                using var bg = new SolidBrush(Color.FromArgb(52, 152, 219));
                using var pen = new Pen(Color.White, 1.5f);
                var rect = new RectangleF(x, y, S, S);
                g.FillEllipse(bg, rect);
                g.DrawEllipse(pen, rect);
            }

            private static void DrawPurpleBadge(Graphics g, float x, float y)
            {
                using var bg = new SolidBrush(Color.FromArgb(155, 89, 182));
                using var pen = new Pen(Color.White, 1.5f);
                var rect = new RectangleF(x, y, S, S);
                g.FillEllipse(bg, rect);
                g.DrawEllipse(pen, rect);
            }

            public TwoBadgesAttributes(AIProviderComponentBase owner) : base(owner) { }

            protected override IEnumerable<(Action<Graphics, float, float> draw, string label)> GetAdditionalBadges()
            {
                yield return (DrawBlueBadge, "Sample badge 1");
                yield return (DrawPurpleBadge, "Sample badge 2");
            }
        }
    }
}
