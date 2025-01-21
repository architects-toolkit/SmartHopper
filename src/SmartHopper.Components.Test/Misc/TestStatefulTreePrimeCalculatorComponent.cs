/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * Portions of this code adapted from:
 * https://github.com/specklesystems/GrasshopperAsyncComponent
 * Apache License 2.0
 * Copyright (c) 2021 Speckle Systems
 */

using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Components.Test.Misc
{
    public class TestStatefulTreePrimeCalculatorComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("E2DB56F0-C597-432C-9774-82DF431CC848");
        protected override System.Drawing.Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public TestStatefulTreePrimeCalculatorComponent()
            : base("Test Stateful Tree Prime Calculator", "TEST-STATEFUL-TREE-PRIME",
                  "Test component for StatefulAsyncComponentBase - Calculates the nth prime number.",
                  "SmartHopper", "Examples")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Number", "N", "Which n-th prime number. Minimum 1, maximum one million.", GH_ParamAccess.item);
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
            private int _nthPrime = 100;
            private long _result = -1;
            private readonly TestStatefulTreePrimeCalculatorComponent _parent;

            public TestStatefulTreePrimeCalculatorWorker(
            TestStatefulTreePrimeCalculatorComponent parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
            : base(parent, addRuntimeMessage)
            {
                _parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA)
            {
                int n = 100;
                DA.GetData(0, ref n);
                _nthPrime = Math.Max(1, Math.Min(n, 1000000));
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                int count = 0;
                long a = 2;

                while (count < _nthPrime)
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
                    }
                    a++;

                    // Add small delay to prevent UI freeze
                    if (count % 100 == 0)
                    {
                        await Task.Delay(1, token);
                    }
                }

                _result = --a;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                _parent.SetPersistentOutput("Output", _result, DA);
                message = $"Found {_nthPrime}th prime: {_result}";
            }
        }
    }
}
