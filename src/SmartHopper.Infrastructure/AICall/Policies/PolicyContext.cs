/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AICall.Policies
{
    /// <summary>
    /// Context object shared across request/response policies in a single execution.
    /// </summary>
    public sealed class PolicyContext
    {
        /// <summary>
        /// The request being processed.
        /// </summary>
        public AIRequestCall Request { get; init; }

        /// <summary>
        /// The response being processed (response phase only).
        /// </summary>
        public AIReturn Response { get; init; }

        /// <summary>
        /// Provider id for convenience.
        /// </summary>
        public string Provider => this.Request?.Provider ?? string.Empty;

        /// <summary>
        /// Model id for convenience.
        /// </summary>
        public string Model => this.Request?.Model ?? string.Empty;

        /// <summary>
        /// Effective capability for the call.
        /// </summary>
        public AICapability Capability => this.Request?.Capability ?? AICapability.None;

        /// <summary>
        /// Structured diagnostics emitted by policies.
        /// </summary>
        public List<AIRuntimeMessage> Diagnostics { get; } = new List<AIRuntimeMessage>();
    }
}
