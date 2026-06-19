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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Infrastructure.AICall.Validation
{
    /// <summary>
    /// Typed validator contract for validating specific models (e.g., tool calls) and returning diagnostics.
    /// Implementations should be side-effect free and perform no network I/O.
    /// </summary>
    /// <typeparam name="T">Type being validated.</typeparam>
    public interface IValidator<T>
    {
        /// <summary>
        /// Severity threshold at or above which the validator considers the validation to fail.
        /// For example, when set to <see cref="SHRuntimeMessageSeverity.Error"/>, any Error message fails the validation.
        /// </summary>
        SHRuntimeMessageSeverity FailOn { get; }

        /// <summary>
        /// Validates the instance and returns a result with messages and IsValid computed per <see cref="FailOn"/>.
        /// </summary>
        /// <param name="instance">Instance to validate.</param>
        /// <param name="context">Ambient validation context (request/response, provider, flags).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation result.</returns>
        Task<ValidationResult> ValidateAsync(T instance, ValidationContext context, CancellationToken cancellationToken);
    }

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
        public List<SHRuntimeMessage> Messages { get; set; } = new List<SHRuntimeMessage>();

        /// <summary>
        /// Optional structured issues including codes and JSON-like path hints.
        /// </summary>
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();

        /// <summary>
        /// Gets the count of error messages by severity for quick gating/metrics.
        /// </summary>
        public int ErrorCount => this.Messages?.Count(m => m?.Severity == SHRuntimeMessageSeverity.Error) ?? 0;

        /// <summary>
        /// Gets the count of warning messages by severity for quick gating/metrics.
        /// </summary>
        public int WarningCount => this.Messages?.Count(m => m?.Severity == SHRuntimeMessageSeverity.Warning) ?? 0;

        /// <summary>
        /// Gets the count of information messages by severity for quick gating/metrics.
        /// </summary>
        public int InfoCount => this.Messages?.Count(m => m?.Severity == SHRuntimeMessageSeverity.Info) ?? 0;

        /// <summary>
        /// Gets a value indicating whether messages have been sanitized to avoid PII leakage.
        /// </summary>
        public bool MessagesSanitized { get; set; }

        /// <summary>
        /// Gets or sets the fallback description when a modality fallback chain was resolved.
        /// Null when no fallback applies.
        /// </summary>
        public string FallbackDescription { get; set; }

        /// <summary>
        /// Gets or sets the resolved fallback chain. Non-null when IsValid and a fallback applies.
        /// </summary>
        public Fallback.FallbackChain FallbackChain { get; set; }

        /// <summary>
        /// Gets a value indicating whether there are any warning messages.
        /// </summary>
        public bool HasWarnings => this.WarningCount > 0;
    }

    /// <summary>
    /// Structured validation issue with optional code and location path.
    /// </summary>
    public sealed class ValidationIssue
    {
        /// <summary>
        /// Gets or sets an optional short code identifying the issue type.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets a JSON-like path to the location of the issue, if applicable.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the severity of the issue.
        /// </summary>
        public SHRuntimeMessageSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the human-readable message describing the issue.
        /// </summary>
        public string Message { get; set; }
    }
}
