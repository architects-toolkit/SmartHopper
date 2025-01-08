// /*
//  * SmartHopper - AI-powered Grasshopper Plugin
//  * Copyright (C) 2024 Marc Roca Musach
//  * 
//  * This library is free software; you can redistribute it and/or
//  * modify it under the terms of the GNU Lesser General Public
//  * License as published by the Free Software Foundation; either
//  * version 3 of the License, or (at your option) any later version.
//  */

// using System;
// using System.Threading;
// using System.Threading.Tasks;
// using Grasshopper.Kernel;
// using SmartHopper.Core.Async.Components;
// using SmartHopper.Core.Async.Core.StateManagement;
// using SmartHopper.Core.Async.Core;
// using SmartHopper.Core.AI;
// using Rhino;

// namespace SmartHopper.Core.Async.Workers
// {
//     /// <summary>
//     /// Base class for AI-powered stateful workers that handle AI API calls and state management.
//     /// </summary>
//     public abstract class AIStatefulWorker : AsyncWorker
//     {
//         protected readonly AIStatefulAsyncComponentBase _aiComponent;
//         protected readonly IComponentStateManager _stateManager;
//         protected CancellationToken _cancellationToken;

//         /// <summary>
//         /// Initializes a new instance of the <see cref="AIStatefulWorker"/> class.
//         /// </summary>
//         /// <param name="progressReporter">Action to report progress messages.</param>
//         /// <param name="parent">The parent AI component.</param>
//         /// <param name="addRuntimeMessage">Action to add runtime messages.</param>
//         protected AIStatefulWorker(
//             Action<string> progressReporter,
//             AIStatefulAsyncComponentBase parent,
//             Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
//             : base(progressReporter, parent, addRuntimeMessage)
//         {
//             _aiComponent = parent;
//             _stateManager = parent.StateManager;
//         }

//         /// <summary>
//         /// Determines whether the worker should start processing.
//         /// </summary>
//         public override bool ShouldStartWork =>
//             _stateManager.CurrentState == ComponentState.Processing;

//         /// <summary>
//         /// Gets a response from the AI provider using the provided messages and cancellation token.
//         /// </summary>
//         /// <param name="messages">The messages to send to the AI provider.</param>
//         /// <param name="token">The cancellation token to cancel the operation.</param>
//         /// <returns>The AI response from the provider.</returns>
//         protected abstract Task<AIResponse> GetResponse(object messages, CancellationToken token);

//         /// <summary>
//         /// Process the AI response and return true if successful.
//         /// </summary>
//         /// <param name="response">The AI response to process.</param>
//         /// <param name="DA">The data access object.</param>
//         /// <returns>True if the response was processed successfully, false otherwise.</returns>
//         protected abstract bool ProcessFinalResponse(AIResponse response, IGH_DataAccess DA);

//         /// <summary>
//         /// Performs the asynchronous work of the worker.
//         /// </summary>
//         /// <param name="token">Cancellation token for the operation.</param>
//         public override async Task DoWorkAsync(CancellationToken token)
//         {
//             _cancellationToken = token;
//             try
//             {
//                 var response = await GetResponse(null, token);
//                 if (response == null)
//                 {
//                     ReportError("Failed to get response from AI provider");
//                     return;
//                 }

//                 _aiComponent.StoreResponseMetrics(response);
                
//                 // Use BeginInvoke to ensure we're on the UI thread for state changes
//                 RhinoApp.InvokeOnUiThread(() =>
//                 {
//                     if (ProcessFinalResponse(response, _aiComponent.DataAccess))
//                     {
//                         _stateManager.TransitionTo(ComponentState.Completed);
//                     }
//                     else
//                     {
//                         _stateManager.HandleRuntimeError();
//                     }
//                 });
//             }
//             catch (Exception ex)
//             {
//                 ReportError($"Error during AI processing: {ex.Message}");
//                 _stateManager.HandleRuntimeError();
//             }
//         }

//         protected override void OnMessageReported(GH_RuntimeMessageLevel level, string message)
//         {
//             base.OnMessageReported(level, message);

//             if (level == GH_RuntimeMessageLevel.Error)
//             {
//                 _stateManager.HandleRuntimeError();
//             }
//         }

//         protected override void OnWorkCompleted()
//         {
//             base.OnWorkCompleted();
//             ReportProgress("AI processing completed");
//         }

//         protected override void OnCancelled()
//         {
//             base.OnCancelled();
//             ReportProgress("AI processing cancelled");
//             _stateManager.TransitionTo(ComponentState.Cancelled);
//         }

//         public override void GatherInput(IGH_DataAccess data)
//         {
//             // Base implementation - override in derived classes to gather specific inputs
//         }

//         public override void SetOutput(IGH_DataAccess data, out string doneMessage)
//         {
//             // Base implementation - override in derived classes to set specific outputs
//             doneMessage = "AI processing completed";
//         }
//     }
// }
