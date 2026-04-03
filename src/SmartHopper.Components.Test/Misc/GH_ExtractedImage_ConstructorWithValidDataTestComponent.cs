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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Types;

namespace SmartHopper.Components.Test.Misc
{
    /// <summary>
    /// Test: GH_ExtractedImage constructor with valid data should be valid.
    /// </summary>
    public class GH_ExtractedImage_ConstructorWithValidDataTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("241459A1-5588-4F33-9A76-BCA7BE0BDB0B");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public GH_ExtractedImage_ConstructorWithValidDataTestComponent()
            : base("Test: GH_ExtractedImage ConstructorWithValidData", "TEST-GH-IMG-VALID",
                  "Tests that GH_ExtractedImage constructor with valid data is valid.",
                  "SmartHopper", "Testing Types")
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
            private readonly GH_ExtractedImage_ConstructorWithValidDataTestComponent _parent;
            private bool _shouldRun = false;

            public Worker(GH_ExtractedImage_ConstructorWithValidDataTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var base64 = CreateValidBase64Png();
                    var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
                    var ghImage = new GH_ExtractedImage(image);

                    if (ghImage.IsValid)
                    {
                        _success = new GH_Boolean(true);
                        _message = new GH_String("✓ PASS: Constructor with valid data is valid");
                    }
                    else
                    {
                        _success = new GH_Boolean(false);
                        _message = new GH_String("✗ FAIL: Expected valid, got invalid");
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

            private static string CreateValidBase64Png()
            {
                using (var bitmap = new Bitmap(1, 1))
                {
                    using (var ms = new MemoryStream())
                    {
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }
    }
}
