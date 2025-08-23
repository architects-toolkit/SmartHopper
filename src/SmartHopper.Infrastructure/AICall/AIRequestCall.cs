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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Represents a fully-specified AI request that providers can execute, including
    /// provider information, model resolution, capability validation, and request body.
    /// </summary>
    public class AIRequestCall : AIRequestBase
    {
        private AICapability capability = AICapability.Text2Text;

        /// <inheritdoc/>
        public override AICapability Capability
        {
            get => this.GetEffectiveCapabilities(out _);
            set => this.capability = value;
        }

        /// <summary>
        /// Gets or sets the HTTP method to use for the request.
        /// </summary>
        public string HttpMethod { get; set; } = "POST";

        /// <summary>
        /// Gets or sets the authentication method to use for the request.
        /// </summary>
        public string Authentication { get; set; } = "bearer";

        /// <summary>
        /// Gets or sets the content type to use for the request.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets the encoded request for the specified provider.
        /// </summary>
        public string EncodedRequestBody
        {
            get
            {
                var (valid, _) = this.IsValid();
                if (valid)
                {
                    return this.ProviderInstance.Encode(this);
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<AIRuntimeMessage> Errors) IsValid()
        {
            var messages = new List<AIRuntimeMessage>();

            var (baseValid, baseErrors) = base.IsValid();
            if (!baseValid)
            {
                messages.AddRange(baseErrors);
            }

            if (string.IsNullOrEmpty(this.Provider))
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Provider is required"));
            }

            if (!this.Capability.HasInput() || !this.Capability.HasOutput())
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Capability field is required with both input and output capabilities"));
            }

            if (this.ProviderInstance == null)
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, $"Unknown provider '{this.Provider}'"));
            }

            var effectiveCapability = this.GetEffectiveCapabilities(out var capabilityNotes);

            // Append any capability notes (informational)
            if (capabilityNotes.Count > 0)
            {
                messages.AddRange(capabilityNotes);
            }

            // Validate resolved model against required capability
            var resolvedModel = this.Model; // Triggers provider-scoped selection
            if (string.IsNullOrEmpty(resolvedModel))
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, $"No capable model found for provider '{this.Provider}' with capability {effectiveCapability.ToString()}"));
            }
            else if (!string.IsNullOrEmpty(this.RequestedModel))
            {
                // If user requested a model and it is known but incompatible, warn and indicate fallback
                var knownRequested = ModelManager.Instance.GetCapabilities(this.Provider, this.RequestedModel);
                if (knownRequested != null && !knownRequested.HasCapability(effectiveCapability) && !string.Equals(this.RequestedModel, resolvedModel, StringComparison.Ordinal))
                {
                    messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Info, AIRuntimeMessageOrigin.Validation, $"Requested model '{this.RequestedModel}' is not capable of {effectiveCapability.ToString()}; using '{resolvedModel}' instead"));
                }
                else if (knownRequested == null && !string.Equals(this.RequestedModel, resolvedModel, StringComparison.Ordinal))
                {
                    // Requested model is unknown; inform user that a fallback model is being used
                    messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Warning, AIRuntimeMessageOrigin.Validation, $"Requested model '{this.RequestedModel}' is unknown; using '{resolvedModel}' instead"));
                }
            }

            if (string.IsNullOrEmpty(this.Endpoint))
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Endpoint is required"));
            }

            if (string.IsNullOrEmpty(this.HttpMethod))
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "HttpMethod is required"));
            }

            if (string.IsNullOrEmpty(this.Authentication))
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Authentication method is required"));
            }

            if (this.Body == null)
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Body is required"));
            }
            else
            {
                if (effectiveCapability.HasFlag(AICapability.JsonOutput) && string.IsNullOrEmpty(this.Body.JsonOutputSchema))
                {
                    messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "JsonOutput capability requires a non-empty JsonOutputSchema"));
                }

                var (bodyOk, bodyErr) = this.Body.IsValid();
                if (!bodyOk)
                {
                    messages.AddRange(bodyErr);
                }
            }

            var hasErrors = messages.Count(m => m.Severity == AIRuntimeMessageSeverity.Error) > 0;

            return (!hasErrors, messages);
        }

        /// <summary>
        /// Executes the request and gets the result. By default, it doesn't trigger the tool processing.
        /// </summary>
        /// <returns>The result of the request in <see cref="AIReturn"/> format.</returns>
        public override async Task<AIReturn> Exec()
        {
            return await this.Exec(false).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the request and gets the result.
        /// </summary>
        /// <param name="processTools">A value indicating whether to process the tool calls.</param>
        /// <returns>The result of the request in <see cref="AIReturn"/> format.</returns>
        public async Task<AIReturn> Exec(bool processTools)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Debug.WriteLine($"[AIRequest.Do] Loading Call method from {this.Provider} with model '{this.Model}' and tools filtered by {this.Body?.ToolFilter ?? "null"}");

            try
            {
                // Validate early to avoid provider calls when request is invalid
                var (rqOk, rqErrors) = this.IsValid();
                if (!rqOk)
                {
                    stopwatch.Stop();
                    return this.BuildErrorReturn("Request validation failed");
                }

                // Guard against missing provider
                if (this.ProviderInstance == null)
                {
                    stopwatch.Stop();
                    var errorResult = new AIReturn();
                    errorResult.CreateProviderError("Provider is missing", this);
                    return errorResult;
                }

                // Execute the request from the provider
                var result = await this.ProviderInstance.Call(this).ConfigureAwait(false);

                // Provider returned null result
                if (result == null)
                {
                    stopwatch.Stop();
                    var none = new AIReturn();
                    none.CreateProviderError("Provider returned no response", this);
                    return none;
                }

                if (processTools)
                {
                    var pendingToolCalls = result.Body?.PendingToolCallsList();

                    // TODO: parallel processing of tool calls
                    if (pendingToolCalls != null)
                    {
                        foreach (var toolCall in pendingToolCalls)
                        {
                            // Create an AIToolCall request
                            var toolCallRequest = new AIToolCall();
                            toolCallRequest.FromToolCallInteraction(toolCall, this.Provider, this.Model);

                            // Execute the tool call
                            var toolResult = await toolCallRequest.Exec().ConfigureAwait(false);

                            if (toolResult == null)
                            {
                                // Merge a standardized tool error and continue
                                result.CreateToolError("Tool not found or did not return a value", this);
                                continue;
                            }

                            // Add the tool result to the result body if present
                            var last = toolResult.Body?.GetLastInteraction();
                            if (last != null)
                            {
                                result.Body.AddLastInteraction(last);
                            }

                            // Merge tool messages and error into the main return
                            result.MergeRuntimeMessagesFrom(toolResult, AIRuntimeMessageOrigin.Tool);
                        }
                    }
                }

                // If provider produced no body and no explicit error, standardize it
                if (result.Body == null && string.IsNullOrEmpty(result.ErrorMessage))
                {
                    result.CreateProviderError("Provider returned no response", this);
                }

                return (AIReturn)result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                var errorResult = new AIReturn();
                errorResult.CreateProviderError("Call cancelled or timed out", this);
                return errorResult;
            }
            catch (TimeoutException)
            {
                stopwatch.Stop();
                var errorResult = new AIReturn();
                errorResult.CreateProviderError("Call cancelled or timed out", this);
                return errorResult;
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                // Network-related issues (DNS, connectivity). Keep raw but standardize user-facing message.
                var raw = this.ExtractProviderErrorMessage(ex);
                var errorResult = new AIReturn();
                if (ex.InnerException is SocketException)
                {
                    errorResult.CreateNetworkError(raw, this);
                }
                else
                {
                    // Treat other HttpRequestException as network as well
                    errorResult.CreateNetworkError(raw, this);
                }
                return errorResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                // Preserve raw provider/unknown error and standardize
                var error = this.ExtractProviderErrorMessage(ex);
                var errorResult = new AIReturn();
                errorResult.CreateProviderError(error, this);
                return errorResult;
            }
        }

        /// <summary>
        /// Initializes the call request.
        /// </summary>
        /// <param name="provider">Provider ID to use (for example, the assembly name of the provider).</param>
        /// <param name="model">Model name to target. If incompatible, the provider's default model will be used.</param>
        /// <param name="systemPrompt">System message inserted as the first interaction of the request.</param>
        /// <param name="endpoint">Provider endpoint or route. If null, an empty string will be used.</param>
        /// <param name="capability">Desired capabilities for this request (input/output and options).</param>
        /// <param name="toolFilter">Optional tool filter expression (e.g., "-*" to disable all tools).</param>
        public void Initialize(string provider, string model,  string systemPrompt, string? endpoint, AICapability capability = AICapability.TextOutput, string? toolFilter = null)
        {
            var interactionList = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.System, Content = systemPrompt },
            };

            this.Initialize(provider, model, interactionList, endpoint ?? string.Empty, capability, toolFilter ?? string.Empty);
        }

        /// <summary>
        /// Replace the interactions list from the body.
        /// </summary>
        /// <param name="interactions">New interactions list that replaces the current body interactions.</param>
        public void OverrideInteractions(List<IAIInteraction> interactions)
        {
            this.Body.OverrideInteractions(interactions);
        }

        /// <summary>
        /// Computes the effective capabilities for this request, augmenting with additional flags
        /// implied by the body (e.g., JsonOutput when a schema is provided, FunctionCalling when tools are requested).
        /// Returns the effective capabilities and a list of informational notes describing adjustments.
        /// </summary>
        /// <param name="notes">Output list populated with informational messages about inferred capabilities.</param>
        private AICapability GetEffectiveCapabilities(out List<AIRuntimeMessage> notes)
        {
            notes = new List<AIRuntimeMessage>();
            var effective = this.capability;

            // If body requires JSON output but capability lacks it, add it (informational)
            if (this.Body?.RequiresJsonOutput() == true && !effective.HasFlag(AICapability.JsonOutput))
            {
                effective |= AICapability.JsonOutput;
                notes.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Info, AIRuntimeMessageOrigin.Validation, "Body requires JSON output but Capability lacks JsonOutput - treating request as JsonOutput"));
            }

            // If tools are requested but capability lacks FunctionCalling, add it (informational)
            if (!string.IsNullOrEmpty(this.Body?.ToolFilter) && this.Body?.ToolFilter != "-*" && !effective.HasFlag(AICapability.FunctionCalling))
            {
                effective |= AICapability.FunctionCalling;
                notes.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Info, AIRuntimeMessageOrigin.Validation, "Tool filter provided but Capability lacks FunctionCalling - treating request as requiring FunctionCalling"));
            }

            return effective;
        }

        /// <summary>
        /// Returns the provider exception message as-is, without sanitization or parsing.
        /// </summary>
        /// <param name="ex">The thrown exception.</param>
        /// <returns>The raw exception message.</returns>
        private string ExtractProviderErrorMessage(Exception ex)
        {
            // Prefer inner exception message if available (providers often wrap exceptions)
            var msg = ex.InnerException?.Message ?? ex.Message;
            return string.IsNullOrEmpty(msg) ? "Unknown error" : msg;
        }
    }
}
