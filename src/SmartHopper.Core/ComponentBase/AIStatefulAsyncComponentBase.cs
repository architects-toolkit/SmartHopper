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
using Grasshopper.Kernel.Types;
using SmartHopper.Core.Utils;
using SmartHopper.Core.Async.Workers;
using SmartHopper.Core.Async.Core;
using SmartHopper.Core.Async.Core.StateManagement;
using SmartHopper.Config.Models;
using SmartHopper.Config.Providers;
using SmartHopper.Config.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for AI-powered stateful asynchronous Grasshopper components.
    /// Provides integrated state management, parallel processing, messaging, and persistence capabilities.
    /// </summary>
    public abstract class AIAsyncStatefulComponentBase : AsyncComponentBase 
    {
        private readonly IStateManager _stateManager;
        private readonly IParallelProcessor _parallelProcessor;
        private readonly IMessagingService _messagingService;
        private readonly IAIService _aiService;
        private readonly ICancellationManager _cancellationManager;
        private readonly IErrorHandler _errorHandler;
        private readonly Dictionary<string, object> _persistentOutputs;

        /// <summary>
        /// Creates a new instance of the AI-powered stateful async component.
        /// </summary>
        /// <param name="name">The component's display name</param>
        /// <param name="nickname">The component's nickname</param>
        /// <param name="description">Description of the component's functionality</param>
        /// <param name="category">Category in the Grasshopper toolbar</param>
        /// <param name="subCategory">Subcategory in the Grasshopper toolbar</param>
        /// <param name="stateManager">Service for managing component state</param>
        /// <param name="parallelProcessor">Service for handling parallel operations</param>
        /// <param name="messagingService">Service for user communication</param>
        /// <param name="aiService">Service for AI operations</param>
        /// <param name="cancellationManager">Service for handling cancellation</param>
        /// <param name="errorHandler">Service for error handling</param>
        protected AIAsyncStatefulComponentBase(
            string name,
            string nickname,
            string description,
            string category,
            string subCategory,
            IStateManager stateManager,
            IParallelProcessor parallelProcessor,
            IMessagingService messagingService,
            IAIService aiService,
            ICancellationManager cancellationManager,
            IErrorHandler errorHandler)
            : base(name, nickname, description, category, subCategory)
        {
            _stateManager = stateManager;
            _parallelProcessor = parallelProcessor;
            _messagingService = messagingService;
            _aiService = aiService;
            _cancellationManager = cancellationManager;
            _errorHandler = errorHandler;
            _persistentOutputs = new Dictionary<string, object>();
        }

        /// <summary>
        /// Called when the component is loaded from a file.
        /// Handles restoration of persistent data and initialization.
        /// </summary>
        /// <remarks>
        /// This method is sealed to ensure proper persistence handling.
        /// Override OnComponentLoaded for custom loading behavior.
        /// </remarks>
        public sealed override void LoadComponent()
        {
            base.LoadComponent();
            RestorePersistentOutputs();
            OnComponentLoaded();
        }

        /// <summary>
        /// Called after component and persistent data are loaded.
        /// Override this method to add custom initialization logic.
        /// </summary>
        /// <remarks>
        /// Use this method instead of LoadComponent to ensure proper persistence handling.
        /// </remarks>
        protected virtual void OnComponentLoaded()
        {
            // Override this in derived classes for additional loading behavior
        }

        /// <summary>
        /// Main solving method for the component.
        /// Handles the execution flow and persistence of results.
        /// </summary>
        /// <remarks>
        /// This method is sealed to ensure proper persistence and error handling.
        /// Override OnSolveInstance for custom solving logic.
        /// </remarks>
        protected sealed override void SolveInstance(IGH_DataAccess DA)
        {
            try
            {
                // Call the derived class's solve implementation
                OnSolveInstance(DA);
                StorePersistentOutputs(DA);
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError(ex);
                throw;
            }
        }

        /// <summary>
        /// Implements the component's solving logic.
        /// Override this method to define the component's behavior.
        /// </summary>
        /// <param name="DA">Provides access to the component's input and output parameters</param>
        /// <remarks>
        /// This is where you implement your component's main functionality.
        /// Use DA.GetData to retrieve inputs and DA.SetData to assign outputs.
        /// </remarks>
        protected abstract void OnSolveInstance(IGH_DataAccess DA);

        /* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
        /* PERSISTENT DATA MANAGER * * * * * * * * * * * * * * * * * * * * * * * * * */
        /* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

        /// <summary>
        /// Restores all persistent outputs to their respective parameters.
        /// Called during component loading.
        /// </summary>
        private void RestorePersistentOutputs()
        {
            for (int i = 0; i < Params.Output.Count; i++)
            {
                var param = Params.Output[i];
                var savedValue = GetPersistentOutput<object>(param.Name);
                if (savedValue != null)
                {
                    // Add the saved data as volatile data
                    param.AddVolatileData(new GH_Path(0), 0, savedValue);
                }
            }
        }

        /// <summary>
        /// Stores all current output values for persistence.
        /// Called after solving the component.
        /// </summary>
        private void StorePersistentOutputs(IGH_DataAccess DA)
        {
            for (int i = 0; i < Params.Output.Count; i++)
            {
                var param = Params.Output[i];
                object value = null;
                
                // Get the output value that was just set
                if (DA.GetData(i, ref value))
                {
                    // Store it for persistence
                    SetPersistentOutput(param.Name, value);
                }
            }
        }

        /// <summary>
        /// Writes the component's persistent data to the Grasshopper file.
        /// </summary>
        /// <param name="writer">The writer to use for serialization</param>
        protected sealed override void Write(GH_IWriter writer)
        {
            base.Write(writer);
            
            // Store number of outputs
            writer.SetInt32("OutputCount", _persistentOutputs.Count);
            
            // Store each output with its parameter name
            int index = 0;
            foreach (var kvp in _persistentOutputs)
            {
                writer.SetString($"ParamName_{index}", kvp.Key);
                
                // Store the type information
                var type = kvp.Value?.GetType();
                writer.SetString($"ParamType_{index}", type?.FullName ?? "null");
                
                // Store the value based on its type
                if (kvp.Value != null)
                {
                    if (type == typeof(int))
                        writer.SetInt32($"Value_{index}", (int)kvp.Value);
                    else if (type == typeof(double))
                        writer.SetDouble($"Value_{index}", (double)kvp.Value);
                    else if (type == typeof(string))
                        writer.SetString($"Value_{index}", (string)kvp.Value);
                    else if (type == typeof(bool))
                        writer.SetBoolean($"Value_{index}", (bool)kvp.Value);
                    else
                        writer.SetString($"Value_{index}", JsonSerializer.Serialize(kvp.Value));
                }
                
                index++;
            }
        }
        
        /// <summary>
        /// Reads the component's persistent data from the Grasshopper file.
        /// </summary>
        /// <param name="reader">The reader to use for deserialization</param>
        protected sealed override void Read(GH_IReader reader)
        {
            base.Read(reader);
            
            _persistentOutputs.Clear();
            
            // Read number of outputs
            if (!reader.ItemExists("OutputCount")) return;
            int count = reader.GetInt32("OutputCount");
            
            // Read each output
            for (int i = 0; i < count; i++)
            {
                string paramName = reader.GetString($"ParamName_{i}");
                string typeName = reader.GetString($"ParamType_{i}");
                
                if (typeName == "null")
                {
                    _persistentOutputs[paramName] = null;
                    continue;
                }
                
                // Get the type
                Type type = Type.GetType(typeName);
                if (type == null) continue;
                
                // Read value based on type
                object value = null;
                if (type == typeof(int))
                    value = reader.GetInt32($"Value_{i}");
                else if (type == typeof(double))
                    value = reader.GetDouble($"Value_{i}");
                else if (type == typeof(string))
                    value = reader.GetString($"Value_{i}");
                else if (type == typeof(bool))
                    value = reader.GetBoolean($"Value_{i}");
                else
                {
                    string json = reader.GetString($"Value_{i}");
                    value = JsonSerializer.Deserialize(json, type);
                }
                
                _persistentOutputs[paramName] = value;
            }
        }

        /// <summary>
        /// Stores a value in the persistent storage.
        /// </summary>
        /// <param name="paramName">Name of the parameter to store</param>
        /// <param name="value">Value to store</param>
        /// <remarks>
        /// This method is protected to allow derived classes to manually store values if needed.
        /// However, values are automatically stored after solving, so manual storage is rarely necessary.
        /// </remarks>
        protected void SetPersistentOutput(string paramName, object value)
        {
            _persistentOutputs[paramName] = value;
        }
        
        /// <summary>
        /// Retrieves a value from persistent storage.
        /// </summary>
        /// <typeparam name="T">The type of value to retrieve</typeparam>
        /// <param name="paramName">Name of the parameter to retrieve</param>
        /// <param name="defaultValue">Value to return if the parameter is not found</param>
        /// <returns>The stored value or defaultValue if not found</returns>
        /// <remarks>
        /// This method is protected to allow derived classes to manually retrieve values if needed.
        /// However, values are automatically restored during loading, so manual retrieval is rarely necessary.
        /// </remarks>
        protected T GetPersistentOutput<T>(string paramName, T defaultValue = default)
        {
            if (_persistentOutputs.TryGetValue(paramName, out object value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }
    }
}