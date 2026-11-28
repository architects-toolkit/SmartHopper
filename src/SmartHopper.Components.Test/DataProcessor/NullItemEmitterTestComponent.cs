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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Components.Test.DataProcessor
{
    /// <summary>
    /// Emits a <see cref="GH_Structure{GH_String}"/> whose branches contain
    /// literal <see langword="null"/> elements — the D21 trigger shape. Wire the
    /// <c>Text</c> output into any stateful component input to reproduce the
    /// "downstream of an empty/null output" scenario end to end: the downstream
    /// processing plan must survive the nulls instead of throwing an NRE during
    /// branch fingerprinting.
    /// </summary>
    public class NullItemEmitterTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("31072FDC-55B1-4685-B9E9-5A765EC81E23");

        protected override Bitmap Icon => null;

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public NullItemEmitterTestComponent()
            : base(
                "Test Null Item Emitter",
                "TEST-NULL-EMIT",
                "Emits a text tree containing literal null elements for downstream D21 regression testing.",
                "SmartHopper Tests",
                "Testing Data")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager) { }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "Text tree containing literal null elements.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Success", "S", "True if at least one null element was injected into the emitted tree.", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Diagnostic messages.", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Structure<GH_String> _resultTree = new GH_Structure<GH_String>();
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly NullItemEmitterTestComponent _parent;

            public Worker(NullItemEmitterTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
            }

            public override Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var path0 = new GH_Path(0);
                    var path1 = new GH_Path(1);

                    var tree = new GH_Structure<GH_String>();
                    tree.Append(new GH_String("alpha"), path0);
                    var nulls0 = NullTreeBuilder.AppendNull(tree, path0);
                    tree.Append(new GH_String("omega"), path0);
                    var nulls1 = NullTreeBuilder.AppendNull(tree, path1);

                    var injected = nulls0 + nulls1;
                    this._resultTree = tree;
                    this._success = new GH_Boolean(injected >= 2);
                    this._messages.Add(new GH_String($"Emitted {{0}} = [alpha, null, omega] (nulls={nulls0}) and {{1}} = [null] (nulls={nulls1})."));
                    this._messages.Add(new GH_String(injected >= 2 ? "Null injection succeeded." : "Null injection failed: the tree did not retain null elements."));

                    Logger.For<Worker>().Trace($"[NullItemEmitter] Emitted tree with {injected} null element(s) across {tree.PathCount} path(s)");
                }
                catch (Exception ex)
                {
                    this._success = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Exception: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                return Task.CompletedTask;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Text", this._resultTree, DA);
                this._parent.SetPersistentOutput("Success", this._success, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                message = this._success.Value ? "Emitted null-bearing tree" : "Emission failed";
            }
        }
    }
}
