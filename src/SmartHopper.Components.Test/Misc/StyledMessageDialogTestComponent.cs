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

/*
 * StyledMessageDialogTest.cs
 * Standalone test component for verifying StyledMessageDialog functionality.
 * Add this component to Grasshopper to test dialog rendering with various message types and lengths.
 */

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.Dialogs;

namespace SmartHopper.Components.Test.Misc
{
    /// <summary>
    /// Test component for StyledMessageDialog with various message types and scenarios.
    /// </summary>
    public class StyledMessageDialogTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("F5BD0E33-7D54-4273-A4AD-94928BB1EF62");

        protected override Bitmap Icon => null;

        public override GH_Exposure Exposure => GH_Exposure.quinary;

        private int _testCase = 0;

        public StyledMessageDialogTestComponent()
            : base("StyledMessageDialog Test", "DialogTest", "Test StyledMessageDialog rendering with various message types", "SmartHopper Tests", "Testing Base")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Test Case", "T", "Test case to run: 0=Short info, 1=Long info, 2=Warning, 3=Error, 4=Confirmation, 5=Very long text, 6=Multiline", 0);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "Test result", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_String _result = new GH_String("Not run");
            private readonly StyledMessageDialogTestComponent _parent;

            public Worker(StyledMessageDialogTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                DA.GetData(0, ref this._parent._testCase);

                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    // Run dialog on UI thread
                    await Task.Run(
                        () => RhinoApp.InvokeOnUiThread(
                            new Action(() =>
                            {
                                this.RunTest(this._parent._testCase);
                            })),
                        token).ConfigureAwait(false);

                    this._result = new GH_String($"Test case {this._parent._testCase} completed");
                }
                catch (OperationCanceledException)
                {
                    this._result = new GH_String("Test cancelled");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Test cancelled.");
                }
                catch (Exception ex)
                {
                    this._result = new GH_String($"Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Result", this._result, DA);
                message = this._result.Value ?? "Test completed";
            }

            private void RunTest(int testCase)
            {
                switch (testCase)
                {
                    case 0:
                        // Short info message
                        StyledMessageDialog.ShowInfo(
                            "This is a short information message.",
                            "Test: Short Info");
                        break;

                    case 1:
                        // Long info message - tests wrapping
                        StyledMessageDialog.ShowInfo(
                            "This is a much longer information message that should demonstrate proper word wrapping " +
                            "in the dialog. The text should automatically wrap to multiple lines when it reaches " +
                            "the width of the dialog, and the dialog height should adjust accordingly. If the text " +
                            "is extremely long, it should become scrollable so the user can read all the content. " +
                            "This message contains approximately 300 characters to test the wrapping behavior.",
                            "Test: Long Info with Wrapping");
                        break;

                    case 2:
                        // Warning message
                        StyledMessageDialog.ShowWarning(
                            "This is a warning message. It should display with an orange warning prefix.",
                            "Test: Warning");
                        break;

                    case 3:
                        // Error message
                        StyledMessageDialog.ShowError(
                            "This is an error message. It should display with a red error prefix.",
                            "Test: Error");
                        break;

                    case 4:
                        // Confirmation dialog
                        bool result = StyledMessageDialog.ShowConfirmation(
                            "This is a confirmation dialog. Do you want to proceed?\n\n" +
                            "Click 'Yes' to confirm or 'No' to cancel.",
                            "Test: Confirmation");

                        // Show result
                        StyledMessageDialog.ShowInfo(
                            $"You clicked: {(result ? "Yes" : "No")}",
                            "Confirmation Result");
                        break;

                    case 5:
                        // Very long text - tests scrolling
                        StyledMessageDialog.ShowInfo(
                            "This is an extremely long message designed to test the scrollable content area. " +
                            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor " +
                            "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud " +
                            "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure " +
                            "dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. " +
                            "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt " +
                            "mollit anim id est laborum.\n\n" +
                            "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque " +
                            "laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi " +
                            "architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas " +
                            "sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione " +
                            "voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit " +
                            "amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut " +
                            "labore et dolore magnam aliquam quaerat voluptatem.\n\n" +
                            "This message should be scrollable because it exceeds the maximum content height.",
                            "Test: Very Long Text with Scrolling");
                        break;

                    case 6:
                        // Multiline with explicit breaks
                        StyledMessageDialog.ShowInfo(
                            "Line 1: First line of text\n" +
                            "Line 2: Second line of text\n" +
                            "Line 3: Third line of text\n" +
                            "Line 4: Fourth line of text\n" +
                            "Line 5: Fifth line of text\n\n" +
                            "This tests explicit line breaks in the message.",
                            "Test: Multiline");
                        break;

                    default:
                        StyledMessageDialog.ShowInfo(
                            "Unknown test case. Please use a value from 0 to 6.",
                            "Test: Invalid");
                        break;
                }
            }
        }
    }
}
