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
    /// Contains tools for image generation using AI.
    /// </summary>
    public class img_generate : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "img_generate";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.ImageOutput;
        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generates an image based on a text prompt using AI image generation models",
                category: "ImageProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prompt"": {
                            ""type"": ""string"",
                            ""description"": ""The text prompt describing the desired image""
                        },
                        ""size"": {
                            ""type"": ""string"",
                            ""description"": ""The size of the generated image (e.g., '1024x1024', '1792x1024', '1024x1792')"",
                            ""default"": ""1024x1024""
                        },
                        ""quality"": {
                            ""type"": ""string"",
                            ""description"": ""The quality of the generated image ('standard' or 'hd')"",
                            ""default"": ""standard""
                        },
                        ""style"": {
                            ""type"": ""string"",
                            ""description"": ""The style of the generated image ('vivid' or 'natural')"",
                            ""default"": ""vivid""
                        }
                    },
                    ""required"": [""prompt""]
                }",
                execute: this.GenerateImageToolWrapper,
                requiredCapabilities: this.toolCapabilityRequirements);
        }



        /// <summary>
        /// Tool wrapper for the GenerateImage function.
        /// </summary>
        /// <param name="toolCall">The tool call information.</param>
        /// <returns>AIReturn with the result.</returns>
        private async Task<AIReturn> GenerateImageToolWrapper(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[ImageTools] Running GenerateImageToolWrapper");

                // Extract parameters
                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                string? prompt = args["prompt"]?.ToString();
                string size = args["size"]?.ToString() ?? "1024x1024";
                string quality = args["quality"]?.ToString() ?? "standard";
                string style = args["style"]?.ToString() ?? "vivid";

                if (string.IsNullOrEmpty(prompt))
                {
                    output.CreateError("Missing required parameter: prompt");
                    return output;
                }

                // Create immutable request body with image interaction
                var requestBody = AIBodyBuilder.Create()
                    .AddImageRequest(prompt: prompt, size: size, quality: quality, style: style)
                    .Build();

                // Create AI request for image generation
                var aiRequest = new AIRequestCall();
                aiRequest.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: this.toolCapabilityRequirements,
                    endpoint: "/images/generations",
                    body: requestBody);

                // Execute the request
                var result = await aiRequest.Exec().ConfigureAwait(false);

                // Check for errors
                if (!result.Success)
                {
                    // Propagate structured messages from AI call
                    output.Messages = result.Messages;
                    return output;
                }

                // Extract image result from interactions
                var resultImageInteraction = result.Body.GetLastInteraction(AIAgent.Assistant) as AIInteractionImage;
                if (resultImageInteraction == null)
                {
                    output.CreateError("No image result received from AI provider");
                    return output;
                }

                // Return the image URL or data (prioritize URL over base64 data for performance)
                string imageResult = resultImageInteraction.ImageUrl != null
                    ? resultImageInteraction.ImageUrl.ToString()
                    : resultImageInteraction.ImageData;

                // Check if we have valid image data
                if (string.IsNullOrEmpty(imageResult))
                {
                    output.CreateError("No image data received from AI provider");
                    return output;
                }

                // Create success result with additional metadata
                var toolResult = new JObject();
                toolResult.Add("result", imageResult);
                toolResult.Add("revisedPrompt", resultImageInteraction.RevisedPrompt ?? string.Empty);
                toolResult.Add("imageSize", resultImageInteraction.ImageSize ?? string.Empty);
                toolResult.Add("imageQuality", resultImageInteraction.ImageQuality ?? string.Empty);
                toolResult.Add("imageStyle", resultImageInteraction.ImageStyle ?? string.Empty);

                // Attach non-breaking result envelope
                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.toolName,
                        type: ToolResultContentType.Image,
                        payloadPath: "result",
                        provider: providerName,
                        model: modelName,
                        toolCallId: toolInfo?.Id));

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.toolName, metrics: result.Metrics, messages: result.Messages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageTools] Error in GenerateImageToolWrapper: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
