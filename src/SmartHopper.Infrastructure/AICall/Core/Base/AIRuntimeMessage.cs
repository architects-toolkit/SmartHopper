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
    /// Machine-readable codes for commonly occurring diagnostics. Use Unknown (0) by default.
    /// The numeric values are stable for serialization and analytics.
    /// </summary>
    public enum AIMessageCode
    {
        // Default/unspecified
        Unknown = 0,

        // Provider and model selection
        ProviderMissing = 1,
        UnknownProvider = 2,
        UnknownModel = 3,
        NoCapableModel = 4,
        CapabilityMismatch = 5,

        // Streaming
        StreamingDisabledProvider = 6,
        StreamingUnsupportedModel = 7,

        // Tools and validation
        ToolValidationError = 8,
        BodyInvalid = 9,
        ReturnInvalid = 10,

        // Network and auth
        NetworkTimeout = 11,
        AuthenticationMissing = 12,
        AuthorizationFailed = 13,
        RateLimited = 14,
    }

    /// <summary>
    /// Structured message model carrying severity, origin, machine-readable code and text.
    /// </summary>
    public class AIRuntimeMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AIRuntimeMessage"/> class with an explicit code.
        /// </summary>
        /// <param name="severity">Message severity.</param>
        /// <param name="origin">Message origin.</param>
        /// <param name="code">Machine-readable code. Defaults to Unknown (0).</param>
        /// <param name="message">Message text.</param>
        /// <param name="surfaceable">Whether this message should be shown to end users in the UI. Defaults to true.</param>
        public AIRuntimeMessage(AIRuntimeMessageSeverity severity, AIRuntimeMessageOrigin origin, AIMessageCode code, string message, bool surfaceable = true)
        {
            this.Severity = severity;
            this.Origin = origin;
            this.Code = code;
            this.Message = message ?? string.Empty;
            this.Surfaceable = surfaceable;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIRuntimeMessage"/> class with default code Unknown (0).
        /// Backward-compatible with existing call sites.
        /// </summary>
        public AIRuntimeMessage(AIRuntimeMessageSeverity severity, AIRuntimeMessageOrigin origin, string message, bool surfaceable = true)
            : this(severity, origin, AIMessageCode.Unknown, message, surfaceable)
        {
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
        /// Gets the machine-readable message code. Defaults to Unknown (0).
        /// </summary>
        public AIMessageCode Code { get; }

        /// <summary>
        /// Gets the message content.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets a value indicating whether this message should be surfaced to the end user in the UI.
        /// Diagnostic-only messages can set this to false.
        /// </summary>
        public bool Surfaceable { get; }
    }
}
