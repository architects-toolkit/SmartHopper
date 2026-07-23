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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Validation;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Infrastructure.AICall.Tools
{
    /// <summary>
    /// Represents a tool call made by an AI model.
    /// </summary>
    public class AIToolCall : AIRequestBase
    {
        // Timeout bounds for tool execution.
        // Sourced from TimeoutDefaults to keep request, provider, and tool layers aligned.
        private const int DEFAULT_TIMEOUT_SECONDS = TimeoutDefaults.DefaultTimeoutSeconds;
        private const int MIN_TIMEOUT_SECONDS = TimeoutDefaults.MinTimeoutSeconds;
        private const int MAX_TIMEOUT_SECONDS = TimeoutDefaults.MaxTimeoutSeconds;

        /// <summary>
        /// Gets or sets the cancellation token for this tool execution.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Gets a value indicating whether the tool call is valid.
        /// </summary>
        /// <returns>A tuple containing a boolean indicating whether the tool call is valid and a list of structured messages.</returns>
        public override (bool IsValid, List<SHRuntimeMessage> Errors) IsValid()
        {
            var messages = new List<SHRuntimeMessage>();

            var (baseValid, baseErrors) = base.IsValid();
            if (!baseValid)
            {
                messages.AddRange(baseErrors);
            }

            // Gate: require exactly one pending tool call in the body
            var pendingCount = this.Body?.PendingToolCallsCount() ?? 0;
            if (pendingCount != 1)
            {
                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.ToolValidationError,
                    "Body must have exactly one pending tool call"));
            }
            else
            {
                // Detailed validation via shared validators (sync-over-async acceptable here; no I/O)
                var call = this.Body.PendingToolCallsList().First();
                var validators = new List<IValidator<AIInteractionToolCall>>
                {
                    new ToolExistsValidator(),
                    new ToolJsonSchemaValidator(),
                    new ToolCapabilityValidator(this.Provider ?? string.Empty, this.Model ?? string.Empty),
                };

                var vctx = new ValidationContext();
                foreach (var v in validators)
                {
                    var res = v.ValidateAsync(call, vctx, CancellationToken.None).GetAwaiter().GetResult();
                    if (res?.Messages != null && res.Messages.Count > 0)
                    {
                        messages.AddRange(res.Messages);
                    }
                }
            }

            var hasErrors = messages.Any(m => m.Severity == SHRuntimeMessageSeverity.Error);

            return (!hasErrors, messages);
        }

        /// <inheritdoc/>
        public override async Task<AIReturn> Exec(CancellationToken cancellationToken = default)
        {
            // If a token was provided to Exec, use it to update the property so the tool manager can use it
            if (cancellationToken != default)
            {
                this.CancellationToken = cancellationToken;
            }

            // Validate early
            var (ok, errors) = this.IsValid();
            if (!ok)
            {
                // Build a detailed tool error including specific validation reasons
                var ret = new AIReturn();
                var errorTexts = (errors ?? new List<SHRuntimeMessage>())
                    .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Message))
                    .Select(m => m.Message)
                    .ToList();
                var combined = errorTexts.Count > 0 ? string.Join(" \n", errorTexts) : "Tool call validation failed";

                // Tool calls may fail before the tool body runs (so tools can't set SkipMetricsValidation themselves).
                // These are local-only calls, so provider/model/finish_reason metrics are not meaningful here.
                this.SkipMetricsValidation = true;
                ret.CreateToolError(combined, this);

                // Attach structured validation messages so UIs and components can surface them
                if (errors != null && errors.Count > 0)
                {
                    ret.Messages = errors;
                }

                return ret;
            }

            try
            {
                // Respect per-request timeout. We cannot cancel the underlying work if the tool ignores cancellation,
                // but we do return a standardized timeout error when exceeded.
                // Resolution chain: explicit per-request value (when > 0) -> shared default.
                // RequestTimeoutPolicy normally resolves this from settings before Exec() runs.
                var timeoutSec = (this.TimeoutSeconds.HasValue && this.TimeoutSeconds.Value > 0)
                    ? this.TimeoutSeconds.Value
                    : DEFAULT_TIMEOUT_SECONDS;
                var clampedTimeout = Math.Min(Math.Max(timeoutSec, MIN_TIMEOUT_SECONDS), MAX_TIMEOUT_SECONDS);
                var execTask = AIToolManager.ExecuteTool(this);
                var completed = await Task.WhenAny(
                    execTask,
                    Task.Delay(TimeSpan.FromSeconds(clampedTimeout))).ConfigureAwait(false);
                if (completed != execTask)
                {
                    var timed = new AIReturn();
                    timed.CreateToolError($"Tool execution exceeded {timeoutSec} seconds", this);
                    return timed;
                }

                var result = await execTask.ConfigureAwait(false);
                if (result == null)
                {
                    var none = new AIReturn();
                    none.CreateToolError("Tool execution returned no result", this);
                    return none;
                }

                // If the tool didn't provide a body and no error messages, standardize it
                if (result.Body == null && !result.Messages.Any(m => m.Severity == SHRuntimeMessageSeverity.Error))
                {
                    result.CreateToolError("Tool execution returned no result", this);
                }

                return result;
            }
            catch (TaskCanceledException)
            {
                var errorResult = new AIReturn();
                errorResult.CreateToolError("Tool execution cancelled or timed out", this);
                return errorResult;
            }
            catch (OperationCanceledException)
            {
                var errorResult = new AIReturn();
                errorResult.CreateToolError("Tool execution cancelled or timed out", this);
                return errorResult;
            }
            catch (TimeoutException)
            {
                var errorResult = new AIReturn();
                errorResult.CreateToolError("Tool execution cancelled or timed out", this);
                return errorResult;
            }
            catch (HttpRequestException ex)
            {
                // Network/DNS issues from tools that make HTTP calls
                var raw = ex.InnerException?.Message ?? ex.Message;
                var errorResult = new AIReturn();
                errorResult.CreateNetworkError(raw, this);
                return errorResult;
            }
            catch (Exception ex)
            {
                // Preserve raw tool error
                var raw = ex.InnerException?.Message ?? ex.Message;
                var errorResult = new AIReturn();
                errorResult.CreateToolError(raw, this);
                return errorResult;
            }
        }

        /// <summary>
        /// Initializes the tool call with the first tool call pending to run from an AI interaction tool call.
        /// </summary>
        public void FromToolCallInteraction(AIInteractionToolCall toolCall, string provider = null, string model = null)
        {
            this.Body = this.Body.WithAppended(toolCall);
            if (provider != null)
            {
                this.Provider = provider;
            }

            if (model != null)
            {
                this.Model = model;
            }
        }

        /// <summary>
        /// Gets the tool call from body.
        /// </summary>
        public AIInteractionToolCall GetToolCall()
        {
            return this.Body.PendingToolCallsList().First();
        }
    }
}
