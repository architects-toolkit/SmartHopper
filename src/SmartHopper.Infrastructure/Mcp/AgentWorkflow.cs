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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System.Collections.Generic;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Represents one canonical SmartHopper tool workflow.
    /// </summary>
    public sealed class AgentWorkflow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentWorkflow"/> class.
        /// </summary>
        /// <param name="name">The stable workflow name.</param>
        /// <param name="description">The workflow description.</param>
        /// <param name="steps">The ordered workflow steps.</param>
        public AgentWorkflow(string name, string description, IReadOnlyList<string> steps)
        {
            this.Name = name;
            this.Description = description;
            this.Steps = steps;
        }

        /// <summary>
        /// Gets the stable workflow name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the workflow description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the ordered workflow steps.
        /// </summary>
        public IReadOnlyList<string> Steps { get; }
    }
}
