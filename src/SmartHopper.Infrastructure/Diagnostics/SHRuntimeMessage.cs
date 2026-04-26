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

namespace SmartHopper.Infrastructure.Diagnostics
{
    /// <summary>
    /// Severity levels for SmartHopper runtime messages.
    /// </summary>
    public enum SHRuntimeMessageSeverity
    {
        /// <summary>Low-level diagnostic, typically hidden from end users.</summary>
        Debug,

        /// <summary>Informational message suitable for end-user visibility.</summary>
        Info,

        /// <summary>Non-fatal issue that the user should be aware of.</summary>
        Warning,

        /// <summary>Error condition that interrupted or degraded the operation.</summary>
        Error,
    }

    /// <summary>
    /// Origin for SmartHopper runtime messages (who emitted it).
    /// </summary>
    public enum SHRuntimeMessageOrigin
    {
        Request,
        Return,
        Provider,
        Tool,
        Network,
        Validation,
        Worker,
    }

    /// <summary>
    /// Machine-readable codes for commonly occurring diagnostics. Use Unknown (0) by default.
    /// The numeric values are stable for serialization and analytics.
    /// </summary>
    public enum SHMessageCode
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

        // Batch processing
        BatchItemError = 20,
        BatchItemCanceled = 21,
        BatchItemExpired = 22,
        BatchItemFinishReason = 23,

        // JSON structured output
        SchemaRequiredAutoAdded = 30,

        // Data tree processing
        TreePathMismatch = 40,
        TreeBranchOmitted = 41,
        TreeNoMatchingPaths = 42,

        // Worker execution
        WorkerException = 50,
        WorkerCancelled = 51,

        // Tool call results
        ToolNoResult = 60,
        ToolInvalidResult = 61,
        ToolExecutionError = 62,

        // Input validation (worker-level)
        InputMissing = 70,
        InputInvalid = 71,

        // File/web I/O
        ConversionFailed = 80,
        ContentEmpty = 81,
    }

    /// <summary>
    /// Structured message model carrying severity, origin, machine-readable code and human-readable text.
    /// Used across the SmartHopper framework for both AI call diagnostics and general worker/component messages.
    /// </summary>
    public class SHRuntimeMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SHRuntimeMessage"/> class with an explicit code.
        /// </summary>
        /// <param name="severity">Message severity.</param>
        /// <param name="origin">Message origin.</param>
        /// <param name="code">Machine-readable code. Defaults to Unknown (0).</param>
        /// <param name="message">Message text.</param>
        /// <param name="surfaceable">Whether this message should be shown to end users in the UI. Defaults to true.</param>
        public SHRuntimeMessage(SHRuntimeMessageSeverity severity, SHRuntimeMessageOrigin origin, SHMessageCode code, string message, bool surfaceable = true)
        {
            this.Severity = severity;
            this.Origin = origin;
            this.Code = code;
            this.Message = message ?? string.Empty;
            this.Surfaceable = surfaceable;
        }

        /// <summary>
        /// Gets the message severity.
        /// </summary>
        public SHRuntimeMessageSeverity Severity { get; }

        /// <summary>
        /// Gets the message origin.
        /// </summary>
        public SHRuntimeMessageOrigin Origin { get; }

        /// <summary>
        /// Gets the machine-readable message code. Defaults to Unknown (0).
        /// </summary>
        public SHMessageCode Code { get; }

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
