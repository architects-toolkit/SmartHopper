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
using SmartHopper.Core.Async.Components;
using SmartHopper.Core.Async.Workers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartHopper.Components.Misc
{
    /// <summary>
    /// Example component implementing SimpleAsyncComponentBase to calculate the nth prime number.
    /// </summary>
    public class PrimeCalculatorComponent : SimpleAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("22C612B0-2C57-47CE-B9FE-E10621F18933");

        protected override System.Drawing.Bitmap Icon => null;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public PrimeCalculatorComponent() 
            : base("The N-th Prime Calculator", "PRIME", 
                  "Calculates the nth prime number.", 
                  "SmartHopper", "Examples")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("N", "N", "Which n-th prime number. Minimum 1, maximum one million.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Output", "O", "The n-th prime number.", GH_ParamAccess.item);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendItem(menu, "Cancel", (s, e) =>
            {
                RequestTaskCancellation();
            });
        }

        protected override AsyncWorker CreateWorker(Action<string> progressReporter)
        {
            return new PrimeCalculatorWorker(progressReporter, this, AddRuntimeMessage);
        }

        private class PrimeCalculatorWorker : AsyncWorker
        {
            private int _nthPrime = 100;
            private long _result = -1;

            public PrimeCalculatorWorker(
                Action<string> progressReporter,
                GH_Component parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(progressReporter, parent, addRuntimeMessage)
            {
            }

            public override void GatherInput(IGH_DataAccess DA)
            {
                int n = 100;
                DA.GetData(0, ref n);
                
                // Clamp values
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

                    ReportProgress($"{((double)count / _nthPrime * 100):F2}%");

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
                DA.SetData(0, _result);
                message = $"Found {_nthPrime}th prime: {_result}";
            }
        }
    }
}
