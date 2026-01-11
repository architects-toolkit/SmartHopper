/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AITools
{
    /// <summary>
    /// Represents an AI-callable tool with metadata and execution function.
    /// </summary>
    public class AITool
    {
        /// <summary>
        /// Gets name of the tool (used for tool calls).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets description of what the tool does.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets category of the tool.
        /// </summary>
        public string Category { get; } = "General";

        /// <summary>
        /// Gets the JSON schema describing the tool's parameters.
        /// </summary>
        public string ParametersSchema { get; }

        /// <summary>
        /// Gets the function to execute the tool with given parameters.
        /// </summary>
        public Func<AIToolCall, Task<AIReturn>> Execute { get; }

        /// <summary>
        /// Gets required capabilities for this tool to function properly.
        /// </summary>
        public AICapability RequiredCapabilities { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AITool"/> class.
        /// Creates a new AI tool.
        /// </summary>
        /// <param name="name">Name of the tool (used for tool calls).</param>
        /// <param name="description">Description of what the tool does.</param>
        /// <param name="category">Category of the tool.</param>
        /// <param name="parametersSchema">JSON schema describing the tool's parameters.</param>
        /// <param name="execute">Function to execute the tool with given parameters.</param>
        /// <param name="requiredCapabilities">Array of capabilities required by this tool (optional, defaults to no requirements).</param>
        public AITool(string name, string description, string category, string parametersSchema, Func<AIToolCall, Task<AIReturn>> execute, AICapability requiredCapabilities = AICapability.None)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Description = description ?? throw new ArgumentNullException(nameof(description));
            this.Category = category ?? throw new ArgumentNullException(nameof(category));
            this.ParametersSchema = parametersSchema ?? throw new ArgumentNullException(nameof(parametersSchema));
            this.Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.RequiredCapabilities = requiredCapabilities;
        }
    }
}
