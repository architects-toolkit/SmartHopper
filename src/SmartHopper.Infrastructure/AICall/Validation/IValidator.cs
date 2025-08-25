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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;

namespace SmartHopper.Infrastructure.AICall.Validation
{
    /// <summary>
    /// Result returned by validators, carrying success flag and structured diagnostics.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the validation passed considering the validator's FailOn threshold.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the collected messages emitted during validation.
        /// </summary>
        public List<AIRuntimeMessage> Messages { get; set; } = new List<AIRuntimeMessage>();

        /// <summary>
        /// Optional structured issues including codes and JSON-like path hints.
        /// </summary>
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();

        /// <summary>
        /// Counts by severity for quick gating/metrics.
        /// </summary>
        public int ErrorCount => this.Messages?.Count(m => m?.Severity == AIRuntimeMessageSeverity.Error) ?? 0;
        public int WarningCount => this.Messages?.Count(m => m?.Severity == AIRuntimeMessageSeverity.Warning) ?? 0;
        public int InfoCount => this.Messages?.Count(m => m?.Severity == AIRuntimeMessageSeverity.Info) ?? 0;

        /// <summary>
        /// Indicates whether messages have been sanitized to avoid PII leakage.
        /// </summary>
        public bool MessagesSanitized { get; set; }
    }

    /// <summary>
    /// Structured validation issue with optional code and location path.
    /// </summary>
    public sealed class ValidationIssue
    {
        public string Code { get; set; }
        public string Path { get; set; }
        public AIRuntimeMessageSeverity Severity { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Typed validator contract for validating specific models (e.g., tool calls) and returning diagnostics.
    /// Implementations should be side-effect free and perform no network I/O.
    /// </summary>
    /// <typeparam name="T">Type being validated.</typeparam>
    public interface IValidator<T>
    {
        /// <summary>
        /// Severity threshold at or above which the validator considers the validation to fail.
        /// For example, when set to <see cref="AIRuntimeMessageSeverity.Error"/>, any Error message fails the validation.
        /// </summary>
        AIRuntimeMessageSeverity FailOn { get; }

        /// <summary>
        /// Validates the instance and returns a result with messages and IsValid computed per <see cref="FailOn"/>.
        /// </summary>
        /// <param name="instance">Instance to validate.</param>
        /// <param name="context">Ambient validation context (request/response, provider, flags).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation result.</returns>
        Task<ValidationResult> ValidateAsync(T instance, ValidationContext context, CancellationToken cancellationToken);
    }
}
