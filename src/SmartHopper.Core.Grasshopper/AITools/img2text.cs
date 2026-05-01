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
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides an AI tool for describing images using vision models (image → text).
    /// </summary>
    public class img2text : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "img2text";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// Requires a vision-capable model that accepts image input and produces text output.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.Image2Text;

        /// <summary>
        /// Default prompt used when no custom prompt is provided.
        /// </summary>
        private readonly string defaultPrompt = "Describe this image in detail. Include all visible content, text, objects, charts, and the apparent purpose or context of the image.";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Describes or analyzes an image using a vision AI model. Provide either an image URL or base64-encoded image data. Returns a text description of the image content.",
                category: "Img",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""imageUrl"": {
                            ""type"": ""string"",
                            ""description"": ""Public URL of the image to analyze (http/https). Use this or imageBase64.""
                        },
                        ""imageBase64"": {
                            ""type"": ""string"",
                            ""description"": ""Base64-encoded image data (without the data URI prefix). Use this or imageUrl.""
                        },
                        ""mimeType"": {
                            ""type"": ""string"",
                            ""description"": ""MIME type of the image when using imageBase64 (e.g. image/png, image/jpeg). Default: image/png."",
                            ""default"": ""image/png""
                        },
                        ""prompt"": {
                            ""type"": ""string"",
                            ""description"": ""Custom instruction for the AI (e.g. 'List all objects visible', 'Describe the architectural style'). Default: Describe this image concisely.""
                        }
                    }
                }",
                execute: this.DescribeImageAsync,
                requiredCapabilities: this.toolCapabilityRequirements);
        }

        /// <summary>
        /// Executes the image description using the configured vision model.
        /// </summary>
        /// <param name="toolCall">The tool call containing provider, model, and arguments.</param>
        /// <returns>An <see cref="AIReturn"/> containing the text description.</returns>
        private async Task<AIReturn> DescribeImageAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine($"[{this.toolName}] Running DescribeImageAsync tool");

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                string imageUrl = args["imageUrl"]?.ToString();
                string imageBase64 = args["imageBase64"]?.ToString();
                string mimeType = args["mimeType"]?.ToString() ?? "image/png";
                string prompt = args["prompt"]?.ToString();

                if (string.IsNullOrWhiteSpace(imageUrl) && string.IsNullOrWhiteSpace(imageBase64))
                {
                    output.CreateToolError("Either 'imageUrl' or 'imageBase64' must be provided.", toolCall);
                    return output;
                }

                string systemPrompt = string.IsNullOrWhiteSpace(prompt)
                    ? this.defaultPrompt
                    : prompt;

                var builder = AIBodyBuilder.Create()
                    .AddSystem(systemPrompt);

                if (!string.IsNullOrWhiteSpace(imageBase64))
                {
                    builder.AddImageInputFromBase64(imageBase64, mimeType);
                }
                else
                {
                    builder.AddImageInput(imageUrl);
                }

                var requestBody = builder.Build();

                var request = new AIRequestCall();
                request.Initialize(
                    provider: toolCall.Provider,
                    model: toolCall.Model,
                    body: requestBody,
                    endpoint: this.toolName,
                    capability: this.toolCapabilityRequirements);
                request.Parameters = toolCall.Parameters;

                var result = await request.Exec().ConfigureAwait(false);

                if (!result.Success)
                {
                    output.Messages = result.Messages;
                    return output;
                }

                var description = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();

                var toolResult = new JObject();
                toolResult.Add("description", description);

                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.toolName,
                        type: ToolResultContentType.Text,
                        payloadPath: "description",
                        provider: toolCall.Provider?.ToString() ?? "Unknown",
                        model: toolCall.Model.ToString(),
                        toolCallId: toolInfo?.Id));

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.toolName, metrics: result.Metrics, messages: result.Messages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.toolName}] Error in DescribeImageAsync: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
