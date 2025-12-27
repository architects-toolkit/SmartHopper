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
 * Portions of this code adapted from:
 * https://github.com/specklesystems/GrasshopperAsyncComponent
 * Apache License 2.0
 * Copyright (c) 2021 Speckle Systems
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Test.Misc
{
    public class TestStatefulTreePrimeCalculatorComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("E2DB56F0-C597-432C-9774-82DF431CC848");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public TestStatefulTreePrimeCalculatorComponent()
            : base("Test Stateful Tree Prime Calculator", "TEST-STATEFUL-TREE-PRIME",
                  "Test component for StatefulAsyncComponentBase - Calculates the nth prime number.",
                  "SmartHopper", "Testing Base")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Number", "N", "Which n-th prime number. Minimum 1, maximum one million.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Output", "O", "The n-th prime number.", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new TestStatefulTreePrimeCalculatorWorker(this, AddRuntimeMessage);
        }

        private class TestStatefulTreePrimeCalculatorWorker : AsyncWorkerBase
        {
            private GH_Structure<GH_Integer> _inputTree;
            private GH_Structure<GH_Number> _result;
            private readonly TestStatefulTreePrimeCalculatorComponent _parent;

            public TestStatefulTreePrimeCalculatorWorker(
            TestStatefulTreePrimeCalculatorComponent parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
            : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
                this._result = new GH_Structure<GH_Number>();
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this._inputTree = new GH_Structure<GH_Integer>();
                DA.GetDataTree(0, out this._inputTree);
                dataCount = this._inputTree.DataCount;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                foreach (var path in this._inputTree.Paths)
                {
                    var branch = this._inputTree.get_Branch(path);
                    var resultBranch = new List<GH_Number>();

                    Debug.WriteLine($"[TestStatefulTreePrimeCalculatorWorker] DoWorkAsync - Processing path {path}");

                    foreach (var item in branch)
                    {
                        token.ThrowIfCancellationRequested();

                        if (item is GH_Integer ghInt)
                        {
                            int n = Math.Max(1, Math.Min(ghInt.Value, 1000000));
                            long result = await CalculateNthPrime(n, token);
                            resultBranch.Add(new GH_Number(result));

                            Debug.WriteLine($"[TestStatefulTreePrimeCalculatorWorker] DoWorkAsync - Calculating nth prime for {n}: {result}");
                        }
                    }

                    this._result.AppendRange(resultBranch, path);
                }
            }

            private async Task<long> CalculateNthPrime(int nthPrime, CancellationToken token)
            {
                int count = 0;
                long a = 2;

                while (count < nthPrime)
                {
                    token.ThrowIfCancellationRequested();

                    long b = 2;
                    bool isPrime = true;

                    while (b * b <= a)
                    {
                        token.ThrowIfCancellationRequested();

                        if (a % b == 0)
                        {
                            isPrime = false;
                            break;
                        }

                        b++;
                    }

                    if (isPrime)
                    {
                        count++;
                        if (count == nthPrime)
                            return a;
                    }

                    a++;
                }

                return -1;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Output", this._result, DA);
                message = $"Found prime";
            }
        }
    }
}
