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
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.AICall.Core.Base
{
    /// <summary>
    /// Severity levels for standardized AI messages.
    /// </summary>
    public enum AIRuntimeMessageSeverity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// Origin for standardized AI messages (who emitted it).
    /// </summary>
    public enum AIRuntimeMessageOrigin
    {
        Request,
        Return,
        Provider,
        Tool,
        Network,
        Validation,
    }

    /// <summary>
    /// Structured message model carrying severity, origin and text.
    /// </summary>
    public class AIRuntimeMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AIRuntimeMessage"/> class.
        /// </summary>
        /// <param name="severity">Message severity.</param>
        /// <param name="origin">Message origin.</param>
        /// <param name="message">Message text.</param>
        public AIRuntimeMessage(AIRuntimeMessageSeverity severity, AIRuntimeMessageOrigin origin, string message)
        {
            this.Severity = severity;
            this.Origin = origin;
            this.Message = message ?? string.Empty;
        }

        /// <summary>
        /// Gets the message severity.
        /// </summary>
        public AIRuntimeMessageSeverity Severity { get; }

        /// <summary>
        /// Gets the message origin.
        /// </summary>
        public AIRuntimeMessageOrigin Origin { get; }

        /// <summary>
        /// Gets the message content.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Formats to legacy string representation with standardized prefix.
        /// </summary>
        public string ToLegacyString()
        {
            var sev = this.Severity switch
            {
                AIRuntimeMessageSeverity.Error => "(Error)",
                AIRuntimeMessageSeverity.Warning => "(Warning)",
                _ => "(Info)",
            };

            // Keep severity first for existing UI sorting; include origin tag for context
            return $"{sev}[{this.Origin}] {this.Message}";
        }
    }
}
