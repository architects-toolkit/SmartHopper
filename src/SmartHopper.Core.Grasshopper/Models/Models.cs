/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
#if WINDOWS
using System.Drawing;
#else
using Eto.Drawing;
#endif
using Grasshopper.Kernel;
using SmartHopper.Config.Models;

namespace SmartHopper.Core.Grasshopper.Models
{
    public class ComponentProperties
    {
        public string Name { get; set; }
        public Guid InstanceGuid { get; set; }
        public Guid ComponentGuid { get; set; }
        public string Type { get; set; }
        public string ObjectType { get; set; }
        public Dictionary<string, ComponentProperty> Properties { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Errors { get; set; }
        public PointF Pivot { get; set; }
        public bool Selected { get; set; }
    }

    public class ComponentProperty
    {
        public object Value { get; set; }
        public string Type { get; set; }
        public string HumanReadable { get; set; }
    }

    public class Connection
    {
        public Guid ComponentId { get; set; }
        public string ParamName { get; set; }
    }

    public class ConnectionPairing
    {
        public Connection From { get; set; }
        public Connection To { get; set; }
    }

    public class GrasshopperDocument
    {
        public List<ComponentProperties> Components { get; set; }
        public List<ConnectionPairing> Connections { get; set; }
    }

    /// <summary>
    /// Generic result type for AI evaluations, providing a standard interface between tools and components
    /// </summary>
    /// <typeparam name="T">The type of the result value, typically a Grasshopper data type</typeparam>
    public class AIEvaluationResult<T>
    {
        /// <summary>
        /// The raw response from the AI
        /// </summary>
        public AIResponse Response { get; set; }

        /// <summary>
        /// The processed result value
        /// </summary>
        public T Result { get; set; }

        /// <summary>
        /// Error message if any occurred during evaluation
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Level of the error message
        /// </summary>
        public GH_RuntimeMessageLevel ErrorLevel { get; set; }

        /// <summary>
        /// Whether the evaluation was successful
        /// </summary>
        public bool Success => string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Creates a new successful result
        /// </summary>
        /// <param name="response">The AI response</param>
        /// <param name="result">The processed result value</param>
        public static AIEvaluationResult<T> CreateSuccess(AIResponse response, T result)
        {
            return new AIEvaluationResult<T>
            {
                Response = response,
                Result = result
            };
        }

        /// <summary>
        /// Creates a new error result
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="level">The error level</param>
        /// <param name="response">Optional AI response that may have caused the error</param>
        public static AIEvaluationResult<T> CreateError(string message, GH_RuntimeMessageLevel level, AIResponse response = null)
        {
            return new AIEvaluationResult<T>
            {
                Response = response,
                ErrorMessage = message,
                ErrorLevel = level
            };
        }
    }
}

