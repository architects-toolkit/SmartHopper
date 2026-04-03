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
    /// Test component for GH_ExtractedImage type.
    /// Tests serialization, casting, and validation of extracted images.
    /// </summary>
    public class GH_ExtractedImageTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("165AD80F-DA97-4DAB-903A-0E884593C21D");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public GH_ExtractedImageTestComponent()
            : base("Test GH_ExtractedImage", "TEST-GH-IMG",
                  "Tests GH_ExtractedImage type: serialization, casting, and validation.",
                  "SmartHopper", "Testing Types")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager) { }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "True if all tests pass.", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test result messages.", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly GH_ExtractedImageTestComponent _parent;

            public Worker(GH_ExtractedImageTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                _parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var testsPassed = 0;
                    var testsFailed = 0;

                    // Test 1: DefaultConstructor_IsNotValid
                    try
                    {
                        var ghImage = new GH_ExtractedImage();
                        if (!ghImage.IsValid)
                        {
                            _messages.Add(new GH_String("✓ DefaultConstructor_IsNotValid"));
                            testsPassed++;
                        }
                        else
                        {
                            _messages.Add(new GH_String("✗ DefaultConstructor_IsNotValid: Expected invalid"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _messages.Add(new GH_String($"✗ DefaultConstructor_IsNotValid: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 2: Constructor_WithValidData_IsValid
                    try
                    {
                        var base64 = CreateValidBase64Png();
                        var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
                        var ghImage = new GH_ExtractedImage(image);
                        if (ghImage.IsValid)
                        {
                            _messages.Add(new GH_String("✓ Constructor_WithValidData_IsValid"));
                            testsPassed++;
                        }
                        else
                        {
                            _messages.Add(new GH_String("✗ Constructor_WithValidData_IsValid: Expected valid"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _messages.Add(new GH_String($"✗ Constructor_WithValidData_IsValid: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 3: IsValid_NullBase64_ReturnsFalse
                    try
                    {
                        var image = new ExtractedImage("img-1", null, "image/png", "Page 1", 1);
                        var ghImage = new GH_ExtractedImage(image);
                        if (!ghImage.IsValid)
                        {
                            _messages.Add(new GH_String("✓ IsValid_NullBase64_ReturnsFalse"));
                            testsPassed++;
                        }
                        else
                        {
                            _messages.Add(new GH_String("✗ IsValid_NullBase64_ReturnsFalse: Expected invalid"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _messages.Add(new GH_String($"✗ IsValid_NullBase64_ReturnsFalse: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 4: CastFrom_PlainString_CreatesFromBase64
                    try
                    {
                        var base64 = CreateValidBase64Png();
                        var ghImage = new GH_ExtractedImage();
                        if (ghImage.CastFrom(base64) && ghImage.Value != null)
                        {
                            _messages.Add(new GH_String("✓ CastFrom_PlainString_CreatesFromBase64"));
                            testsPassed++;
                        }
                        else
                        {
                            _messages.Add(new GH_String("✗ CastFrom_PlainString_CreatesFromBase64: Cast failed"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _messages.Add(new GH_String($"✗ CastFrom_PlainString_CreatesFromBase64: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 5: CastTo_String_ReturnsBase64
                    try
                    {
                        var base64 = CreateValidBase64Png();
                        var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
                        var ghImage = new GH_ExtractedImage(image);
                        string result = null;
                        if (ghImage.CastTo(ref result) && result == base64)
                        {
                            _messages.Add(new GH_String("✓ CastTo_String_ReturnsBase64"));
                            testsPassed++;
                        }
                        else
                        {
                            _messages.Add(new GH_String("✗ CastTo_String_ReturnsBase64: Cast failed or value mismatch"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _messages.Add(new GH_String($"✗ CastTo_String_ReturnsBase64: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 6: ScriptVariable_ValidBase64_ReturnsBitmap
                    try
                    {
                        var base64 = CreateValidBase64Png();
                        var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
                        var ghImage = new GH_ExtractedImage(image);
                        var scriptVar = ghImage.ScriptVariable();
                        if (scriptVar is Bitmap)
                        {
                            _messages.Add(new GH_String("✓ ScriptVariable_ValidBase64_ReturnsBitmap"));
                            testsPassed++;
                        }
                        else
                        {
                            _messages.Add(new GH_String("✗ ScriptVariable_ValidBase64_ReturnsBitmap: Expected Bitmap"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _messages.Add(new GH_String($"✗ ScriptVariable_ValidBase64_ReturnsBitmap: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 7: Write_Read_RoundTrip
                    try
                    {
                        var base64 = CreateValidBase64Png();
                        var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
                        var ghImage1 = new GH_ExtractedImage(image);

                        var writer = new GH_IO.Serialization.GH_LooseChunk("ExtractedImage");
                        ghImage1.Write(writer);
                        var bytes = writer.Serialize_Binary();

                        var reader = new GH_IO.Serialization.GH_LooseChunk("ExtractedImage");
                        reader.Deserialize_Binary(bytes);
                        var ghImage2 = new GH_ExtractedImage();
                        ghImage2.Read(reader);

                        if (ghImage1.Value.Id == ghImage2.Value.Id && ghImage1.Value.Base64Data == ghImage2.Value.Base64Data)
                        {
                            _messages.Add(new GH_String("✓ Write_Read_RoundTrip"));
                            testsPassed++;
                        }
                        else
                        {
                            _messages.Add(new GH_String("✗ Write_Read_RoundTrip: Data mismatch after round-trip"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _messages.Add(new GH_String($"✗ Write_Read_RoundTrip: {ex.Message}"));
                        testsFailed++;
                    }

                    _success = new GH_Boolean(testsFailed == 0);
                    _messages.Insert(0, new GH_String($"GH_ExtractedImage Tests: {testsPassed} passed, {testsFailed} failed"));

                    await Task.Yield();
                }
                catch (OperationCanceledException)
                {
                    _success = new GH_Boolean(false);
                    _messages.Add(new GH_String("Operation was cancelled."));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Test cancelled.");
                }
                catch (Exception ex)
                {
                    _success = new GH_Boolean(false);
                    _messages.Add(new GH_String($"Exception: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                _parent.SetPersistentOutput("Success", _success, DA);
                _parent.SetPersistentOutput("Messages", _messages, DA);
                message = _success.Value ? "All GH_ExtractedImage tests passed" : "Some tests failed";
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
