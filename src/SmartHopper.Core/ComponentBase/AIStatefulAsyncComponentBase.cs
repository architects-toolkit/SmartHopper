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

/*
 * Base class for all AI-powered SmartHopper components.
 * This class provides the fundamental structure for components that need to perform
 * asynchronous, showing an State message and connect to AI, while maintaining
 * Grasshopper's component lifecycle.
 */

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using GH_IO.Serialization;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Diagnostics;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for AI-powered stateful asynchronous Grasshopper components.
    /// Provides integrated state management, parallel processing, messaging, and persistence capabilities.
    /// </summary>
    public abstract class AIAsyncStatefulComponentBase : AsyncComponentBase
    {
        #region PRIVATE FIELDS

        // These services will be implemented later
        //private readonly IParallelProcessor _parallelProcessor;
        //private readonly IAIService _aiService;

        // PERSISTENT DATA MANAGEMENT private fields
        private readonly Dictionary<string, object> _persistentOutputs;
        private readonly Dictionary<string, Type> _persistentDataTypes;

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Creates a new instance of the AI-powered stateful async component.
        /// </summary>
        /// <param name="name">The component's display name</param>
        /// <param name="nickname">The component's nickname</param>
        /// <param name="description">Description of the component's functionality</param>
        /// <param name="category">Category in the Grasshopper toolbar</param>
        /// <param name="subCategory">Subcategory in the Grasshopper toolbar</param>
        protected AIAsyncStatefulComponentBase(
            string name,
            string nickname,
            string description,
            string category,
            string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            _persistentOutputs = new Dictionary<string, object>();
            _persistentDataTypes = new Dictionary<string, Type>();
        }

        #endregion

        // --------------------------------------------------
        //                COMPONENT DEFINITION
        // --------------------------------------------------
        // 
        // This section of code is responsible for managing
        // the component's lifecycle and state transitions,
        // implementing the necessary methods for a
        // Grasshopper Component.

        #region INPUTS AND OUTPUTS

        /// <summary>
        /// Registers input parameters for the component.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Allow derived classes to add their specific inputs
            RegisterAdditionalInputParams(pManager);

            pManager.AddBooleanParameter("Run?", "R", "Set this parameter to true to run the component.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Register component-specific input parameters, to define in derived classes.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected abstract void RegisterAdditionalInputParams(GH_InputParamManager pManager);

        /// <summary>
        /// Registers output parameters for the component.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Allow derived classes to add their specific outputs
            RegisterAdditionalOutputParams(pManager);
        }

        /// <summary>
        /// Register component-specific output parameters, to define in derived classes.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected abstract void RegisterAdditionalOutputParams(GH_OutputParamManager pManager);

        #endregion

        #region COMPONENT LIFECYCLE

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
                // Check Run parameter
                bool run = false;
                DA.GetData("Run?", ref run);

                if (!run)
                {
                    // Restore last output
                    RestorePersistentOutputs(DA);
                    return;
                }

                // Call the base class's solve implementation for normal execution
                base.SolveInstance(DA);
            }
            catch (Exception ex)
            {
                //_errorHandler.HandleError(ex);
                throw;
            }
        }

        protected sealed override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
        }

        #endregion


        // --------------------------------------------------
        //             PERSISTENT DATA MANAGEMENT
        // --------------------------------------------------
        // 
        // This section of code is responsible for storing
        // and retrieving persistent data for the component.

        #region PERSISTENCE DATA MANAGEMENT

        /// <summary>
        /// Restores all persistent outputs to their respective parameters.
        /// Called when the component is added to a document.
        /// </summary>
        protected void RestorePersistentOutputs(IGH_DataAccess DA)
        {
            Debug.WriteLine("[AIAsyncStatefulComponentBase] [PersistentData] Restoring persistent outputs");
            for (int i = 0; i < Params.Output.Count; i++)
            {
                var param = Params.Output[i];
                var savedValue = GetPersistentOutput<object>(param.Name);
                if (savedValue != null)
                {
                    try 
                    {
                        // Create a new IGH_Goo instance if the value isn't already one
                        IGH_Goo gooValue;
                        if (savedValue is IGH_Goo existingGoo)
                        {
                            gooValue = existingGoo;
                        }
                        else
                        {
                            // Try to create a new goo instance of the appropriate type
                            var gooType = param.Type;
                            gooValue = GH_Convert.ToGoo(savedValue);
                            if (gooValue == null)
                            {
                                // If direct conversion fails, try creating instance and casting
                                gooValue = (IGH_Goo)Activator.CreateInstance(gooType);
                                gooValue.CastFrom(savedValue);
                            }
                        }

                        // Add the properly typed goo value
                        SetPersistentOutput(param.Name, gooValue, DA);
                        Debug.WriteLine("[AIAsyncStatefulComponentBase] [PersistentData] Successfully restored output '" + param.Name + "' with value '" + gooValue + "'");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[AIAsyncStatefulComponentBase] [PersistentData] Failed to restore output '" + param.Name + "': " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Writes the component's persistent data to the Grasshopper file.
        /// </summary>
        /// <param name="writer">The writer to use for serialization</param>
        /// <returns>True if the write operation succeeds, false if it fails or an exception occurs</returns>
        public sealed override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer))
                return false;
            
            try
            {
                // Store number of outputs
                writer.SetInt32("OutputCount", _persistentOutputs.Count);
                
                // Store each output with its parameter name
                int index = 0;
                foreach (var kvp in _persistentOutputs)
                {
                    writer.SetString($"ParamName_{index}", kvp.Key);
                    
                    // Get the expected type from _persistentDataTypes
                    Type expectedType = null;
                    if (_persistentDataTypes.TryGetValue(kvp.Key, out expectedType))
                    {
                        writer.SetString($"ParamType_{index}", expectedType.AssemblyQualifiedName);
                        object valueToStore = kvp.Value;
                        
                        // If it's a GH_ObjectWrapper, extract the inner value
                        if (kvp.Value?.GetType()?.FullName == "Grasshopper.Kernel.Types.GH_ObjectWrapper")
                        {
                            var valueProperty = kvp.Value.GetType().GetProperty("Value");
                            if (valueProperty != null)
                            {
                                valueToStore = valueProperty.GetValue(kvp.Value);
                            }
                        }
                        
                        // Store the value based on its type
                        if (valueToStore != null)
                        {
                            if (valueToStore is int intValue)
                                writer.SetInt32($"Value_{index}", intValue);
                            else if (valueToStore is double doubleValue)
                                writer.SetDouble($"Value_{index}", doubleValue);
                            else if (valueToStore is string stringValue)
                                writer.SetString($"Value_{index}", stringValue);
                            else if (valueToStore is bool boolValue)
                                writer.SetBoolean($"Value_{index}", boolValue);
                            else
                                writer.SetString($"Value_{index}", JsonSerializer.Serialize(valueToStore));
                        }

                        Debug.WriteLine($"[AIAsyncStatefulComponentBase] [PersistentData] Stored output '{kvp.Key}' with value '{valueToStore}' of stored type '{expectedType?.FullName}'");
                        
                        index++;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the component's persistent data from the Grasshopper file.
        /// </summary>
        /// <param name="reader">The reader to use for deserialization</param>
        /// <returns>True if the read operation succeeds, false if it fails, required data is missing, or an exception occurs</returns>
        public sealed override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader))
                return false;

            _persistentOutputs.Clear();

            // Read number of outputs
            if (!reader.ItemExists("OutputCount"))
                return false;

            int count = reader.GetInt32("OutputCount");

            try
            {
                // Read each output
                for (int i = 0; i < count; i++)
                {
                    string paramName = reader.GetString($"ParamName_{i}");
                    string typeName = reader.GetString($"ParamType_{i}");
                    Debug.WriteLine($"[AIAsyncStatefulComponentBase] [Read] Attempting to deserialize parameter '{paramName}' of type '{typeName}'");

                    if (typeName == "null")
                    {
                        _persistentOutputs[paramName] = null;
                        continue;
                    }

                    // Get the type and store it in _persistentDataTypes
                    Type type = Type.GetType(typeName);
                    if (type == null)
                    {
                        Debug.WriteLine($"[AIAsyncStatefulComponentBase] [Read] Type '{typeName}' could not be found");
                        continue;
                    }
                    
                    // Store the type for future reference
                    _persistentDataTypes[paramName] = type;
                    Debug.WriteLine($"[AIAsyncStatefulComponentBase] [Read] Registered type {type.Name} for parameter {paramName}");
                    
                    // Read value based on type
                    object value;
                    // Handle primitive types directly
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
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        // If it's a Grasshopper type, create instance and set value
                        if (type?.FullName?.StartsWith("Grasshopper.Kernel.Types.") == true)
                        {
                            value = Activator.CreateInstance(type);
                            var targetValueProperty = type.GetProperty("Value");
                            if (targetValueProperty != null)
                            {
                                var deserializedValue = JsonSerializer.Deserialize(reader.GetString($"Value_{i}"), targetValueProperty.PropertyType, options);
                                targetValueProperty.SetValue(value, deserializedValue);
                            }
                        }
                        else
                        {
                            value = JsonSerializer.Deserialize(reader.GetString($"Value_{i}"), type, options);
                        }
                    }

                    Debug.WriteLine($"[AIAsyncStatefulComponentBase] [Read] Successfully deserialized to type {type?.FullName}");
                    
                    _persistentOutputs[paramName] = value;

                    Debug.WriteLine("[AIAsyncStatefulComponentBase] [PersistentData] Restored output '" + paramName + "' to value '" + value + "'");
                }

                return true;
            }
            catch
            {
                return false;
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
        protected void SetPersistentOutput(string paramName, object value, IGH_DataAccess DA)
        {
            try
            {
                // Store the value in persistent storage
                _persistentOutputs[paramName] = value;

                // Find the output parameter
                var param = Params.Output.FirstOrDefault(p => p.Name == paramName);
                if (param != null)
                {
                    // Convert to IGH_Goo if needed
                    if (!(value is IGH_Goo))
                    {
                        value = Grasshopper.Kernel.GH_Convert.ToGoo(value);
                    }

                    // Set the data through DA
                    DA.SetData(param.Name, value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AIAsyncStatefulComponentBase] [PersistentData] Failed to set output '" + paramName + "': " + ex.Message);
            }
        }
        
        /// <summary>
        /// Retrieves a value from persistent storage.
        /// </summary>
        /// <typeparam name="T">The type of value to retrieve</typeparam>
        /// <param name="paramName">Name of the parameter to retrieve</param>
        /// <param name="defaultValue">Value to return if the parameter is not found</param>
        /// <returns>The stored value or defaultValue if not found</returns>
        protected T GetPersistentOutput<T>(string paramName, T defaultValue = default)
        {
            if (_persistentOutputs.TryGetValue(paramName, out object value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        #endregion

    }
}