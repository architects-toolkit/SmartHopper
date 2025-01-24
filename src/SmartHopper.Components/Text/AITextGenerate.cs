/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
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
using SmartHopper.Components.Properties;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System.Collections.Generic;
using Grasshopper.Documentation;

namespace SmartHopper.Components.Text
{
    public class AITextGenerate : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("EB073C7A-A500-4265-A45B-B1BFB38BA58E");
        protected override System.Drawing.Bitmap Icon => Resources.textgenerate;
        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AITextGenerate()
            : base("AI Text Generate", "AITextGenerate",
                  "Generate text using LLM.\nIf a tree structure is provided, prompts and instructions will only match within the same branch paths.",
                  "SmartHopper", "Text")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "The prompt to send to the AI", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Specify what the AI should do when receiving the prompt", GH_ParamAccess.tree, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "The AI's response", GH_ParamAccess.tree);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AITextGenerateWorker(this, AddRuntimeMessage);
        }

        private class AITextGenerateWorker : AsyncWorkerBase
        {
            private GH_Structure<GH_String> _inputTree;
            private GH_Structure<GH_String> _result;
            private readonly AITextGenerate _parent;

            public AITextGenerateWorker(
            AITextGenerate parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
            : base(parent, addRuntimeMessage)
            {
                _parent = parent;
                _result = new GH_Structure<GH_String>();
            }

            public override void GatherInput(IGH_DataAccess DA)
            {
                DA.GetDataTree(0, out GH_Structure<GH_String> promptTree);

                DA.GetDataTree(1, out GH_Structure<GH_String> instructionsTree);

                _inputTree = new GH_Structure<GH_String>();
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                //foreach (var path in _inputTree.Paths)
                //{
                //    var branch = _inputTree.get_Branch(path);
                //    var resultBranch = new List<GH_Number>();

                //    Debug.WriteLine($"[TestStatefulTreePrimeCalculatorWorker] DoWorkAsync - Processing path {path}");

                //    foreach (var item in branch)
                //    {
                //        token.ThrowIfCancellationRequested();

                //        if (item is GH_Integer ghInt)
                //        {
                //            int n = Math.Max(1, Math.Min(ghInt.Value, 1000000));
                //            long result = await CalculateNthPrime(n, token);
                //            resultBranch.Add(new GH_Number(result));

                //            Debug.WriteLine($"[TestStatefulTreePrimeCalculatorWorker] DoWorkAsync - Calculating nth prime for {n}: {result}");
                //        }
                //    }

                //    _result.AppendRange(resultBranch, path);
                //}
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
                _parent.SetPersistentOutput("Output", _result, DA);
                message = $"Found prime";
            }
        }
    }
}
