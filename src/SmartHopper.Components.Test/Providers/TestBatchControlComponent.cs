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
using System.Linq;
using Grasshopper.Kernel;
using SmartHopper.Providers.Test;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Canvas control plane for <see cref="TestProvider"/>'s deterministic batch
    /// behavior. Wire the knobs from panels/toggles, set <c>Apply?</c> to true to
    /// write them into <see cref="TestBatchControl"/>, then drive stateful batch
    /// components whose provider is <c>TestProvider</c>. The <c>Events</c> output
    /// exposes the provider's recorded SUBMIT/POLL/CANCEL/DOWNLOAD/PARSE trail
    /// for assertions; re-solve this component (e.g. via <c>gh_run</c>) to
    /// refresh it after a scenario runs.
    /// </summary>
    public class TestBatchControlComponent : GH_Component
    {
        public override Guid ComponentGuid => new Guid("54FC1136-9779-4DA1-9FFE-1EAD444E08E2");

        protected override Bitmap Icon => null;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public TestBatchControlComponent()
            : base(
                "Test Batch Control",
                "TEST-BATCH-CTL",
                "Configures the deterministic TestProvider batch behavior (delays, failures) and reports its event trail.",
                "SmartHopper Tests",
                "Testing Providers")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Submit Delay", "SD", "Milliseconds SubmitBatchAsync waits before returning the submission handle.", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Complete After", "CA", "Milliseconds after submission before GetBatchStatusAsync reports Completed.", GH_ParamAccess.item, 1000);
            pManager.AddIntegerParameter("Omit Indices", "OI", "Zero-based custom-id positions dropped from the completed results (per-item failures).", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Throw On Submit", "TS", "When true, SubmitBatchAsync throws instead of returning a submission.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Fail Batch", "FB", "When true, GetBatchStatusAsync reports Failed once the completion deadline passes.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Apply?", "A", "When true, the knobs above are written into TestBatchControl.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Reset?", "R", "When true, knobs and the recorded event/counter surface reset to defaults before Apply is processed.", GH_ParamAccess.item, false);

            this.Params.Input[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Config", "C", "The currently applied TestBatchControl configuration.", GH_ParamAccess.item);
            pManager.AddTextParameter("Counters", "N", "Provider call counters: submits/polls/cancels/downloads.", GH_ParamAccess.item);
            pManager.AddTextParameter("Events", "E", "Ordered provider event trail (SUBMIT/POLL/CANCEL/DOWNLOAD/PARSE).", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int submitDelay = 0;
            int completeAfter = 1000;
            var omit = new List<int>();
            bool throwOnSubmit = false;
            bool failBatch = false;
            bool apply = false;
            bool reset = false;

            DA.GetData(0, ref submitDelay);
            DA.GetData(1, ref completeAfter);
            DA.GetDataList(2, omit);
            DA.GetData(3, ref throwOnSubmit);
            DA.GetData(4, ref failBatch);
            DA.GetData(5, ref apply);
            DA.GetData(6, ref reset);

            if (reset)
            {
                TestBatchControl.Reset();
            }

            if (apply)
            {
                TestBatchControl.SubmitDelayMs = Math.Max(0, submitDelay);
                TestBatchControl.CompleteAfterMs = Math.Max(0, completeAfter);
                TestBatchControl.OmitResultIndices = omit.Where(i => i >= 0).ToArray();
                TestBatchControl.ThrowOnSubmit = throwOnSubmit;
                TestBatchControl.FailWholeBatch = failBatch;
            }

            DA.SetData(0, $"submitDelay={TestBatchControl.SubmitDelayMs}ms completeAfter={TestBatchControl.CompleteAfterMs}ms omit=[{string.Join(", ", TestBatchControl.OmitResultIndices)}] throw={TestBatchControl.ThrowOnSubmit} failBatch={TestBatchControl.FailWholeBatch}");
            DA.SetData(1, $"submits={TestBatchControl.SubmitCalls} polls={TestBatchControl.PollCalls} cancels={TestBatchControl.CancelCalls} downloads={TestBatchControl.DownloadCalls}");
            DA.SetDataList(2, TestBatchControl.Events.ToArray());
        }
    }
}
