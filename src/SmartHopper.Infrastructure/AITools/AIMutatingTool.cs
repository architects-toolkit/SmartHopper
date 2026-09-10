/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Infrastructure.AITools
{
    /// <summary>
    /// Represents an AI tool that mutates canvas or document state and cannot run in batch mode.
    /// </summary>
    public sealed class AIMutatingTool : AITool
    {
        /// <summary>
        /// Initializes a new mutating tool.
        /// </summary>
        public AIMutatingTool(
            string name,
            string description,
            string category,
            string parametersSchema,
            Func<AIToolCall, Task<AIReturn>> execute,
            AICapability requiredCapabilities = AICapability.None,
            bool enabled = true,
            IReadOnlyList<string>? tags = null,
            string? outputSchema = null,
            AIToolAnnotations? annotations = null,
            AIToolSurface surfaces = AIToolSurface.Chat | AIToolSurface.Direct | AIToolSurface.Mcp)
            : base(
                name,
                description,
                category,
                parametersSchema,
                execute,
                requiredCapabilities,
                null,
                true,
                enabled,
                tags,
                outputSchema,
                annotations,
                surfaces & ~AIToolSurface.Batch)
        {
        }
    }
}
