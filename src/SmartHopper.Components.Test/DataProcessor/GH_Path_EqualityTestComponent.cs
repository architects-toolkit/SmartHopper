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
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Test.DataProcessor
{
    /// <summary>
    /// Test GH_Path equality comparison works correctly.
    /// </summary>
    public class GH_Path_EqualityTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("E19C7C94-E670-4F97-8061-CE81078C29B7");

        protected override Bitmap Icon => null;

        public override GH_Exposure Exposure => GH_Exposure.octonary;

        public GH_Path_EqualityTestComponent()
            : base(
                "Test GH_Path Equality",
                "TEST-GH-PATH-EQ",
                "Tests that GH_Path equality comparison works correctly.",
                "SmartHopper",
                "Testing Data")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            // Run? parameter is provided by StatefulComponentBase
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "True if test passes.", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "M", "Test result message.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _success = new GH_Boolean(false);
            private GH_String _message = new GH_String(string.Empty);
            private readonly GH_Path_EqualityTestComponent _parent;
            private bool _shouldRun = false;

            public Worker(GH_Path_EqualityTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
                DA.GetData("Run?", ref this._shouldRun);
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this._shouldRun)
                {
                    this._success = new GH_Boolean(false);
                    this._message = new GH_String("Test not run (Run = false)");
                    await Task.Yield();
                    return;
                }

                try
                {
                    var path1 = new GH_Path(0);
                    var path2 = new GH_Path(0);
                    var path3 = new GH_Path(1);

                    bool equal = path1 == path2;
                    bool notEqual = path1 != path3;

                    if (equal && notEqual)
                    {
                        this._success = new GH_Boolean(true);
                        this._message = new GH_String("✓ PASS: GH_Path equality works correctly");
                    }
                    else
                    {
                        this._success = new GH_Boolean(false);
                        this._message = new GH_String($"✗ FAIL: Expected equal=true and notEqual=true, got equal={equal} and notEqual={notEqual}");
                    }
                }
                catch (Exception ex)
                {
                    this._success = new GH_Boolean(false);
                    this._message = new GH_String($"✗ FAIL: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Success", this._success, DA);
                this._parent.SetPersistentOutput("Message", this._message, DA);
                message = this._message.Value;
            }
        }
    }
}
