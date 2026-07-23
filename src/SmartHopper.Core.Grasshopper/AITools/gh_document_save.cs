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
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for saving the current Grasshopper document.
    /// </summary>
    public class gh_document_save : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_document_save";

        /// <summary>
        /// Returns AI tools for saving the current Grasshopper document.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Save the current Grasshopper document. If no filePath is provided, the document is saved to its existing location. Provide a full file path to save a copy or unnamed document.",
                category: "Document",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filePath"": {
                            ""type"": [""string"", ""null""],
                            ""description"": ""Optional full file path to save the document. If omitted, the current document path is used.""
                        }
                    }
                }",
                execute: this.GhDocumentSaveToolAsync,
                mutatesCanvas: false,
                tags: new[] { "document", "save", "file" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""filePath"": { ""type"": [""string"", ""null""] }, ""saved"": { ""type"": ""boolean"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: false));
        }

        private Task<AIReturn> GhDocumentSaveToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var filePath = args["filePath"]?.ToString();

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    filePath = null;
                }
                else
                {
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }

                var savedPath = CanvasAccess.SaveDocument(filePath);
                var saved = !string.IsNullOrWhiteSpace(savedPath);

                if (!saved)
                {
                    output.CreateError("Could not save the document. No active document or no file path available.");
                    return Task.FromResult(output);
                }

                var toolResult = new JObject
                {
                    ["saved"] = saved,
                    ["filePath"] = savedPath,
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return Task.FromResult(output);
            }
        }
    }
}