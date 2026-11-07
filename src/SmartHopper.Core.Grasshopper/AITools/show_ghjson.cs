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
using System.Threading.Tasks;
using GhJSON.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Interaction;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Display tool that hands a GhJSON definition to the user as a persistent chat
    /// artifact card: collapsed source preview, copy action and — for valid documents —
    /// a place-on-canvas action. The document is validated against the GhJSON schema
    /// first; invalid payloads render as a small red card with the issues listed and
    /// return structured errors to the model so it can fix the document and retry.
    /// It never mutates the document.
    /// </summary>
    public class show_ghjson : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private const string ToolName = "show_ghjson";

        private const int MaxGhJsonLength = 200_000;
        private const int MaxTitleLength = 120;
        private const int MaxReportedErrors = 10;

        /// <summary>
        /// Returns the show_ghjson display tool.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Shows a GhJSON definition to the user as a persistent artifact card in the chat (collapsed source, copy button, and a place-on-canvas action when the document is valid). ALWAYS use this to deliver a GhJSON definition — never paste GhJSON into your reply text. The document is validated against the GhJSON schema first; on failure the call returns structured validation errors — fix the document and call again. It never changes the document.",
                category: "GhJSON.Tools",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""ghjson"": { ""description"": ""The complete GhJSON document — a JSON object or its string form."", ""anyOf"": [{ ""type"": ""object"" }, { ""type"": ""string"" }] },
                        ""title"": { ""type"": ""string"", ""description"": ""Optional short card title, e.g. '10x5 point grid'."" }
                    },
                    ""required"": [""ghjson""]
                }",
                execute: this.ShowGhJsonAsync,
                mutatesCanvas: false,
                tags: new[] { "ghjson", "chat", "artifact", "display", "read-only" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""artifactId"": { ""type"": ""string"" },
                        ""displayed"": { ""type"": ""boolean"" },
                        ""valid"": { ""type"": ""boolean"" },
                        ""error"": { ""type"": ""string"" },
                        ""errors"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                        ""hint"": { ""type"": ""string"" }
                    }
                }",
                annotations: new AIToolAnnotations(
                    readOnlyHint: true,
                    destructiveHint: false,
                    idempotentHint: true,
                    openWorldHint: false,
                    title: "Show GhJSON"));
        }

        private async Task<AIReturn> ShowGhJsonAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                // Local display tool: do not require provider/model/finish_reason metrics.
                toolCall.SkipMetricsValidation = true;

                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var json = ExtractGhJsonString(args["ghjson"]);
                if (string.IsNullOrWhiteSpace(json))
                {
                    output.CreateError("'ghjson' is required.");
                    return output;
                }

                if (json.Length > MaxGhJsonLength)
                {
                    output.CreateError($"The GhJSON document exceeds the {MaxGhJsonLength} character limit.");
                    return output;
                }

                var title = (args["title"]?.ToString() ?? string.Empty).Trim();
                if (title.Length > MaxTitleLength)
                {
                    title = title.Substring(0, MaxTitleLength);
                }

                var validation = GhJson.Validate(json);
                var issues = validation.Errors
                    .Take(MaxReportedErrors)
                    .Select(e => new GhJsonArtifactIssue
                    {
                        Path = e.Path ?? string.Empty,
                        Message = e.Message ?? string.Empty,
                    })
                    .ToList();

                var document = TryParse(json);
                var artifact = new GhJsonArtifact
                {
                    Id = $"ghjson-{Guid.NewGuid():N}",
                    Title = string.IsNullOrWhiteSpace(title) ? null : title,
                    GhJson = PrettyPrint(json),
                    Valid = validation.IsValid,
                    Components = document?["components"] is JArray comps ? comps.Count : 0,
                    Connections = document?["connections"] is JArray conns ? conns.Count : 0,
                    Errors = issues,
                };

                var displayed = false;
                var presenter = toolCall.InvocationContext?.GhJsonArtifactPresenter;
                if (presenter != null)
                {
                    try
                    {
                        await presenter.ShowAsync(artifact, toolCall.CancellationToken).ConfigureAwait(false);
                        displayed = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.For<show_ghjson>().Exception(ex, $"[show_ghjson] Presenter failed: {ex.Message}");
                    }
                }

                var result = new JObject
                {
                    ["artifactId"] = artifact.Id,
                    ["displayed"] = displayed,
                    ["valid"] = validation.IsValid,
                };
                if (!validation.IsValid)
                {
                    result["error"] = "invalid_ghjson";
                    result["errors"] = new JArray(issues.Select(i =>
                        string.IsNullOrEmpty(i.Path) ? i.Message : $"{i.Path}: {i.Message}"));
                    result["hint"] = "Fix the GhJSON and call show_ghjson again with the corrected document.";
                }

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(result, toolInfo.Id, toolInfo.Name);
                output.CreateSuccess(builder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Extracts the GhJSON text from the tool argument. Accepts a JSON string or an
        /// already-parsed object; unlike gh_put this tool does not read file paths —
        /// the payload is meant for display, not for file access.
        /// </summary>
        private static string ExtractGhJsonString(JToken? token)
        {
            if (token == null)
            {
                return string.Empty;
            }

            return token.Type == JTokenType.Object || token.Type == JTokenType.Array
                ? token.ToString(Formatting.None)
                : token.ToString();
        }

        private static JObject? TryParse(string json)
        {
            try
            {
                return JObject.Parse(json);
            }
            catch
            {
                return null;
            }
        }

        private static string PrettyPrint(string json)
        {
            try
            {
                return JToken.Parse(json).ToString(Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }
    }
}
