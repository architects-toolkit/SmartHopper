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
    /// Test: GH_Structure with strings can be created.
    /// </summary>
    public class GH_Structure_WithStringsTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("C3D4E5F6-A7B8-4C9D-0E1F-2A3B4C5D6E7F");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public GH_Structure_WithStringsTestComponent()
            : base("Test: GH_Structure WithStrings", "TEST-GH-STRUCT-STR",
                  "Tests that GH_Structure can be created with strings.",
                  "SmartHopper", "Testing Data")
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
            return new Worker(this, AddRuntimeMessage);
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
                _parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
                DA.GetData("Run?", ref _shouldRun);
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!_shouldRun)
                {
                    _success = new GH_Boolean(false);
                    _message = new GH_String("Test not run (Run = false)");
                    await Task.Yield();
                    return;
                }

                try
                {
                    var tree = new GH_Structure<GH_String>();
                    tree.Append(new GH_String("test"), new GH_Path(0));

                    if (tree.Paths.Count == 1 && tree.DataCount == 1)
                    {
                        _success = new GH_Boolean(true);
                        _message = new GH_String("✓ PASS: GH_Structure with strings created successfully");
                    }
                    else
                    {
                        _success = new GH_Boolean(false);
                        _message = new GH_String($"✗ FAIL: Expected 1 path and 1 item, got {tree.Paths.Count} paths and {tree.DataCount} items");
                    }
                }
                catch (Exception ex)
                {
                    _success = new GH_Boolean(false);
                    _message = new GH_String($"✗ FAIL: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                _parent.SetPersistentOutput("Success", _success, DA);
                _parent.SetPersistentOutput("Message", _message, DA);
                message = _message.Value;
            }
        }
    }
}
