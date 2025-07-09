/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
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
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Test.Misc
{
    public class TestStatefulPrimeCalculatorComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("C2C612B0-2C57-47CE-B9FE-E10621F18935");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public TestStatefulPrimeCalculatorComponent()
            : base("Test Stateful Prime Calculator", "TEST-STATEFUL-PRIME",
                  "Test component for StatefulAsyncComponentBase - Calculates the nth prime number.",
                  "SmartHopper", "Testing")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Number", "N", "Which n-th prime number. Minimum 1, maximum one million.", GH_ParamAccess.item);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Output", "O", "The n-th prime number.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new TestStatefulPrimeCalculatorWorker(this, AddRuntimeMessage);
        }

        private class TestStatefulPrimeCalculatorWorker : AsyncWorkerBase
        {
            private int _nthPrime = 100;
            private long _result = -1;
            private readonly TestStatefulPrimeCalculatorComponent _parent;

            public TestStatefulPrimeCalculatorWorker(
            TestStatefulPrimeCalculatorComponent parent,
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
