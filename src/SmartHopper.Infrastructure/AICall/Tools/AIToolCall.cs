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
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
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

            if (this.Body.Interactions.Count == 0)
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Body cannot be empty"));
            }

            if (this.Body.PendingToolCallsCount() != 1)
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Body must have exactly one pending tool call"));
            }

            foreach (var toolCall in this.Body.PendingToolCallsList())
            {
                if (string.IsNullOrEmpty(toolCall.Name))
                {
                    messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, $"Tool name is required for tool call {toolCall.Id}"));
                }

                if (toolCall.Arguments == null)
                {
                    messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Info, AIRuntimeMessageOrigin.Validation, $"Tool arguments are not set for tool call {toolCall.Id}"));
                }
            }

            var hasErrors = messages.Count(m => m.Severity == AIRuntimeMessageSeverity.Error) > 0;

            return (!hasErrors, messages);
        }

        /// <inheritdoc/>
        public override async Task<AIReturn> Exec()
        {
            // Validate early
            var (ok, errors) = this.IsValid();
            if (!ok)
            {
                return this.BuildErrorReturn("Tool call validation failed");
            }

            try
            {
                var result = await AIToolManager.ExecuteTool(this).ConfigureAwait(false);
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
            this.Body.AddLastInteraction(toolCall);
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
