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
    /// Test GH_Structure with strings can be created.
    /// </summary>
    public class GH_Structure_WithStringsTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("561D6314-0C04-4DC3-AB1F-8C50202E9C75");

        protected override Bitmap Icon => null;

        public override GH_Exposure Exposure => GH_Exposure.octonary;

        public GH_Structure_WithStringsTestComponent()
            : base(
                "Test GH_Structure WithStrings",
                "TEST-GH-STRUCT-STR",
                "Tests that GH_Structure can be created with strings.",
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
            private readonly GH_Structure_WithStringsTestComponent _parent;
            private bool _shouldRun = false;

            public Worker(GH_Structure_WithStringsTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var tree = new GH_Structure<GH_String>();
                    tree.Append(new GH_String("test"), new GH_Path(0));

                    if (tree.Paths.Count == 1 && tree.DataCount == 1)
                    {
                        this._success = new GH_Boolean(true);
                        this._message = new GH_String("✓ PASS: GH_Structure with strings created successfully");
                    }
                    else
                    {
                        this._success = new GH_Boolean(false);
                        this._message = new GH_String($"✗ FAIL: Expected 1 path and 1 item, got {tree.Paths.Count} paths and {tree.DataCount} items");
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
