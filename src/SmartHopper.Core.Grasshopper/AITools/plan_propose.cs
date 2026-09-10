/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Consent;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides the Chat-only copilot plan proposal control tool.
    /// </summary>
    public sealed class plan_propose : IAIToolProvider
    {
        private const int MaxSteps = 20;
        private const int MaxTextLength = 4000;

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                "plan_propose",
                "Propose a user-reviewable plan before a non-trivial multi-step task. Call only when planning materially improves clarity. Approval permits continuing with the approach but never pre-approves later canvas changes.",
                "Control",
                @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""goal"": { ""type"": ""string"" },
                        ""summary"": { ""type"": ""string"" },
                        ""steps"": {
                            ""type"": ""array"",
                            ""minItems"": 1,
                            ""maxItems"": 20,
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""id"": { ""type"": ""string"" },
                                    ""description"": { ""type"": ""string"" },
                                    ""tool"": { ""type"": ""string"" }
                                },
                                ""required"": [""id"", ""description""]
                            }
                        },
                        ""assumptions"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                        ""successCriteria"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }
                    },
                    ""required"": [""goal"", ""summary"", ""steps""]
                }",
                this.ExecuteAsync,
                mutatesCanvas: false,
                tags: new[] { "control", "planning", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""planId"": { ""type"": ""string"" }, ""status"": { ""type"": ""string"" }, ""steps"": { ""type"": ""array"" } } }",
                surfaces: AIToolSurface.Chat);
        }

        private async Task<AIReturn> ExecuteAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                toolCall.SkipMetricsValidation = true;
                var args = toolCall.GetToolCall().GetArgumentsOrEmpty();
                var goal = ReadBounded(args, "goal", required: true);
                var summary = ReadBounded(args, "summary", required: true);
                var stepsToken = args["steps"] as JArray ?? throw new ArgumentException("A plan requires a steps array.");
                if (stepsToken.Count == 0 || stepsToken.Count > MaxSteps)
                {
                    throw new ArgumentException($"A plan must contain between 1 and {MaxSteps} steps.");
                }

                var registeredTools = AIToolManager.GetTools();
                var steps = new List<PlanConsentStep>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var token in stepsToken.OfType<JObject>())
                {
                    var id = ReadBounded(token, "id", required: true);
                    if (!ids.Add(id))
                    {
                        throw new ArgumentException($"Duplicate plan step id '{id}'.");
                    }

                    var toolName = ReadBounded(token, "tool", required: false);
                    AITool? registeredTool = null;
                    if (!string.IsNullOrWhiteSpace(toolName))
                    {
                        if (!registeredTools.TryGetValue(toolName, out registeredTool) ||
                            (registeredTool.Surfaces & AIToolSurface.Chat) == 0)
                        {
                            throw new ArgumentException($"Plan step '{id}' references unavailable Chat tool '{toolName}'.");
                        }
                    }

                    steps.Add(new PlanConsentStep
                    {
                        Id = id,
                        Description = ReadBounded(token, "description", required: true),
                        Tool = string.IsNullOrWhiteSpace(toolName) ? null : toolName,
                        MutatesCanvas = registeredTool?.MutatesCanvas == true,
                    });
                }

                var proposal = new PlanConsentProposal(
                    goal,
                    summary,
                    steps,
                    ReadStringArray(args["assumptions"]),
                    ReadStringArray(args["successCriteria"]));
                var decision = await ConsentGate.RequestAsync(
                    proposal,
                    toolCall.InvocationContext,
                    toolCall.CancellationToken).ConfigureAwait(false);
                var result = new JObject
                {
                    ["planId"] = proposal.Id,
                    ["status"] = decision.Status.ToString(),
                    ["steps"] = JArray.FromObject(steps),
                    ["reason"] = decision.Reason,
                };
                output.CreateSuccess(AIBodyBuilder.Create()
                    .AddToolResult(result, id: toolCall.GetToolCall().Id, name: "plan_propose")
                    .Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateToolError(ex.Message, toolCall);
                return output;
            }
        }

        private static string ReadBounded(JObject source, string name, bool required)
        {
            var value = source[name]?.ToString().Trim() ?? string.Empty;
            if (required && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Plan field '{name}' is required.");
            }

            if (value.Length > MaxTextLength)
            {
                throw new ArgumentException($"Plan field '{name}' exceeds {MaxTextLength} characters.");
            }

            return value;
        }

        private static IReadOnlyList<string> ReadStringArray(JToken? token)
        {
            return token is JArray array
                ? array.Values<string>()
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Length > MaxTextLength ? value.Substring(0, MaxTextLength) : value)
                    .Take(MaxSteps)
                    .ToList()
                : Array.Empty<string>();
        }
    }
}
