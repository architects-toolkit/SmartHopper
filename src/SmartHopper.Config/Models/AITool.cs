/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Config.Models
{
    /// <summary>
    /// Represents an AI-callable tool with metadata and execution function
    /// </summary>
    public class AITool
    {
        /// <summary>
        /// Name of the tool (used for tool calls)
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Description of what the tool does
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// JSON schema describing the tool's parameters
        /// </summary>
        public string ParametersSchema { get; }

        /// <summary>
        /// Function to execute the tool with given parameters
        /// </summary>
        public Func<JObject, Task<object>> Execute { get; }

        /// <summary>
        /// Creates a new AI tool
        /// </summary>
        /// <param name="name">Name of the tool (used for tool calls)</param>
        /// <param name="description">Description of what the tool does</param>
        /// <param name="parametersSchema">JSON schema describing the tool's parameters</param>
        /// <param name="execute">Function to execute the tool with given parameters</param>
        public AITool(string name, string description, string parametersSchema, Func<JObject, Task<object>> execute)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            ParametersSchema = parametersSchema ?? throw new ArgumentNullException(nameof(parametersSchema));
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }
    }
}
