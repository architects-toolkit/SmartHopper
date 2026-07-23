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

using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Unified UI-only diagnostic interaction carrying a <see cref="SHRuntimeMessage"/>-shaped payload
    /// (severity, origin, code, surfaceable flag, content).
    /// Covers what were previously four distinct interaction types (Debug, Info, Warning, Error),
    /// with severity modelled as data rather than as type. Providers must skip all instances of
    /// this class during request encoding — these entries are for UI/diagnostics only and must never
    /// be sent to the AI model.
    /// </summary>
    public class AIInteractionRuntimeMessage : AIInteractionBase, IAIKeyedInteraction, IAIRenderInteraction
    {
        /// <summary>
        /// Gets or sets the diagnostic severity. Also determines the effective <see cref="Agent"/>,
        /// the CSS role class used for rendering and the display name shown in the UI.
        /// </summary>
        public SHRuntimeMessageSeverity Severity { get; set; } = SHRuntimeMessageSeverity.Info;

        /// <summary>
        /// Gets or sets the machine-readable diagnostic code. Defaults to <see cref="SHMessageCode.Unknown"/>.
        /// </summary>
        public SHMessageCode Code { get; set; } = SHMessageCode.Unknown;

        /// <summary>
        /// Gets or sets the origin of this diagnostic. Defaults to <see cref="SHRuntimeMessageOrigin.Return"/>.
        /// </summary>
        public SHRuntimeMessageOrigin Origin { get; set; } = SHRuntimeMessageOrigin.Return;

        /// <summary>
        /// Gets or sets a value indicating whether this diagnostic should be surfaced to the end user.
        /// Defaults to true for Info/Warning/Error and false for Debug (adjusted via
        /// <see cref="CreateDebug(string,AIMetrics)"/> or explicitly by callers).
        /// </summary>
        public bool Surfaceable { get; set; } = true;

        /// <summary>
        /// Gets or sets the human-readable diagnostic text.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets the effective agent for this diagnostic, derived from <see cref="Severity"/>.
        /// The setter is intentionally a no-op — callers should set <see cref="Severity"/> instead.
        /// </summary>
        public override AIAgent Agent
        {
            get => this.Severity switch
            {
                SHRuntimeMessageSeverity.Error => AIAgent.Error,
                SHRuntimeMessageSeverity.Warning => AIAgent.Warning,
                SHRuntimeMessageSeverity.Info => AIAgent.Info,
                SHRuntimeMessageSeverity.Debug => AIAgent.Debug,
                _ => AIAgent.Unknown,
            };
            set { /* Agent is derived from Severity; setter retained for base-class/serialization compatibility. */ }
        }

        /// <summary>
        /// Sets the diagnostic content and optional metrics.
        /// </summary>
        /// <param name="content">The diagnostic text.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        public void SetResult(string content, AIMetrics metrics = null)
        {
            this.Content = content;
            this.Metrics = metrics ?? new AIMetrics();
        }

        /// <summary>
        /// Returns the content of this diagnostic.
        /// </summary>
        /// <returns>The diagnostic content or empty string.</returns>
        public override string ToString()
        {
            return this.Content ?? string.Empty;
        }

        /// <summary>
        /// Projects this interaction into an equivalent <see cref="SHRuntimeMessage"/>.
        /// </summary>
        /// <returns>A new <see cref="SHRuntimeMessage"/> carrying the same metadata.</returns>
        public SHRuntimeMessage ToRuntimeMessage()
        {
            return new SHRuntimeMessage(this.Severity, this.Origin, this.Code, this.Content ?? string.Empty, this.Surfaceable);
        }

        /// <summary>
        /// Creates a runtime-message interaction from an <see cref="SHRuntimeMessage"/>.
        /// </summary>
        /// <param name="message">The runtime message to project into an interaction.</param>
        /// <returns>A new <see cref="AIInteractionRuntimeMessage"/> mirroring the input.</returns>
        public static AIInteractionRuntimeMessage FromRuntimeMessage(SHRuntimeMessage message)
        {
            if (message == null)
            {
                return null;
            }

            return new AIInteractionRuntimeMessage
            {
                Severity = message.Severity,
                Origin = message.Origin,
                Code = message.Code,
                Content = message.Message,
                Surfaceable = message.Surfaceable,
            };
        }

        /// <summary>
        /// Creates a debug-level diagnostic (non-surfaceable by default).
        /// </summary>
        /// <param name="content">The diagnostic text.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        /// <returns>A new <see cref="AIInteractionRuntimeMessage"/> configured for Debug severity.</returns>
        public static AIInteractionRuntimeMessage CreateDebug(string content, AIMetrics metrics = null)
        {
            return new AIInteractionRuntimeMessage
            {
                Severity = SHRuntimeMessageSeverity.Debug,
                Surfaceable = false,
                Content = content,
                Metrics = metrics ?? new AIMetrics(),
            };
        }

        /// <inheritdoc />
        public string GetRoleClassForRender()
        {
            return this.Severity switch
            {
                SHRuntimeMessageSeverity.Error => "error",
                SHRuntimeMessageSeverity.Warning => "warning",
                SHRuntimeMessageSeverity.Info => "info",
                SHRuntimeMessageSeverity.Debug => "debug",
                _ => "info",
            };
        }

        /// <inheritdoc />
        public string GetDisplayNameForRender()
        {
            return this.Severity switch
            {
                SHRuntimeMessageSeverity.Error => "Error",
                SHRuntimeMessageSeverity.Warning => "Warning",
                SHRuntimeMessageSeverity.Info => "Info",
                SHRuntimeMessageSeverity.Debug => "Debug",
                _ => "Info",
            };
        }

        /// <inheritdoc />
        public string GetRawContentForRender()
        {
            return this.Content ?? string.Empty;
        }

        /// <summary>
        /// Runtime-message diagnostics do not include a reasoning section.
        /// </summary>
        /// <returns>An empty string.</returns>
        public string GetRawReasoningForRender()
        {
            return string.Empty;
        }

        /// <inheritdoc />
        public string GetStreamKey()
        {
            var hash = HashUtility.ComputeShortHash(this.Content ?? string.Empty);
            var role = this.GetRoleClassForRender();

            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}:{role}:{hash}";
            }

            return $"{role}:{hash}";
        }

        /// <inheritdoc />
        public string GetDedupKey()
        {
            return this.GetStreamKey();
        }
    }
}
