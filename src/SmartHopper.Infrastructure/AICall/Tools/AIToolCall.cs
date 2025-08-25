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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Validation;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall.Tools
{
    /// <summary>
    /// Represents a tool call made by an AI model.
    /// </summary>
    public class AIToolCall : AIRequestBase
    {
        /// <summary>
        /// Gets a value indicating whether the tool call is valid.
        /// </summary>
        /// <returns>A tuple containing a boolean indicating whether the tool call is valid and a list of structured messages.</returns>
        public override (bool IsValid, List<AIRuntimeMessage> Errors) IsValid()
        {
            var messages = new List<AIRuntimeMessage>();

            var (baseValid, baseErrors) = base.IsValid();
            if (!baseValid)
            {
                messages.AddRange(baseErrors);
            }

            // Gate: require exactly one pending tool call in the body
            var pendingCount = this.Body?.PendingToolCallsCount() ?? 0;
            if (pendingCount != 1)
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
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

            var hasErrors = messages.Count(m => m.Severity == AIRuntimeMessageSeverity.Error) > 0;

            return (!hasErrors, messages);
        }

        /// <inheritdoc/>
        public override async Task<AIReturn> Exec()
        {
            // Validate early
            var (ok, _) = this.IsValid();
            if (!ok)
            {
                return this.BuildErrorReturn("Tool call validation failed");
            }

            try
            {
                // Respect per-request timeout. We cannot cancel the underlying work if the tool ignores cancellation,
                // but we do return a standardized timeout error when exceeded.
                var timeoutSec = this.TimeoutSeconds <= 0 ? 120 : this.TimeoutSeconds;
                var execTask = AIToolManager.ExecuteTool(this);
                var completed = await Task.WhenAny(execTask, Task.Delay(TimeSpan.FromSeconds(Math.Min(Math.Max(timeoutSec, 1), 600)))).ConfigureAwait(false);
                if (completed != execTask)
                {
                    var timed = new AIReturn();
                    timed.CreateToolError("Tool execution cancelled or timed out", this);
                    return timed;
                }

                var result = await execTask.ConfigureAwait(false);
                if (result == null)
                {
                    var none = new AIReturn();
                    none.CreateToolError("Tool execution returned no result", this);
                    return none;
                }

                // If the tool didn't provide a body and no explicit error, standardize it
                if (result.Body == null && string.IsNullOrEmpty(result.ErrorMessage))
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
