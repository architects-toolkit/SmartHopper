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
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Models;
using SmartHopper.Core.Messaging;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Models;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Contains tools for image generation using AI.
    /// </summary>
    public class img_generate : IAIToolProvider
    {
        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "img_generate",
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
                requiredCapabilities: new[] { ModelCapability.TextInput, ModelCapability.ImageOutput });
        }

        /// <summary>
        /// Generates an image from a prompt using AI with a custom GenerateImage function.
        /// </summary>
        /// <param name="prompt">The user's prompt.</param>
        /// <param name="size">The size of the generated image.</param>
        /// <param name="quality">The quality setting for the image.</param>
        /// <param name="style">The style setting for the image.</param>
        /// <param name="generateImage">Custom function to generate image.</param>
        /// <returns>The image URL or data as a GH_String.</returns>
        private static async Task<AIEvaluationResult<GH_String>> GenerateImageAsync(
            GH_String prompt,
            GH_String size,
            GH_String quality,
            GH_String style,
            Func<string, string, string, string, string, Task<AIResponse>> generateImage)
        {
            try
            {
                // Get response using the provided function
                var response = await generateImage(prompt.Value, "", size.Value, quality.Value, style.Value).ConfigureAwait(false);

                // Check for API errors
                if (response.FinishReason == "error")
                {
                    return AIEvaluationResult<GH_String>.CreateError(
                        response.ErrorMessage,
                        GH_RuntimeMessageLevel.Error,
                        response); // Now using AIResponse which is compatible with AIEvaluationResult
                }

                // Return the image URL or data (prioritize URL over base64 data for performance)
                string imageResult = !string.IsNullOrEmpty(response.ImageUrl)
                    ? response.ImageUrl
                    : response.ImageData;
                
                // Check if we have valid image data
                if (string.IsNullOrEmpty(imageResult))
                {
                    return AIEvaluationResult<GH_String>.CreateError(
                        "No image data received from AI provider",
                        GH_RuntimeMessageLevel.Error,
                        response);
                }
                
                // Success case
                return AIEvaluationResult<GH_String>.CreateSuccess(
                    response, // Now using AIResponse which is compatible with AIEvaluationResult
                    new GH_String(imageResult));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageTools] Error in GenerateImageAsync: {ex.Message}");
                return AIEvaluationResult<GH_String>.CreateError(
                    $"Error generating image: {ex.Message}",
                    GH_RuntimeMessageLevel.Error);
            }
        }

        /// <summary>
        /// Tool wrapper for the GenerateImage function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<object> GenerateImageToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[ImageTools] Running GenerateImageToolWrapper");

                // Extract parameters
                string providerName = parameters["provider"]?.ToString() ?? string.Empty;
                string modelName = parameters["model"]?.ToString() ?? string.Empty;
                string? prompt = parameters["prompt"]?.ToString();
                string size = parameters["size"]?.ToString() ?? "1024x1024";
                string quality = parameters["quality"]?.ToString() ?? "standard";
                string style = parameters["style"]?.ToString() ?? "vivid";

                if (string.IsNullOrEmpty(prompt))
                {
                    // Return error object as JObject
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Missing required parameter: prompt"
                    };
                }

                // Execute the tool
                var result = await GenerateImageAsync(
                    new GH_String(prompt),
                    new GH_String(size),
                    new GH_String(quality),
                    new GH_String(style),
                    (promptText, model, imageSize, imageQuality, imageStyle) => AIUtils.GenerateImage(
                        providerName,
                        promptText,
                        model: modelName,
                        size: imageSize,
                        quality: imageQuality,
                        style: imageStyle)
                ).ConfigureAwait(false);

                // Build standardized result as JObject
                var responseObj = new JObject
                {
                    ["success"] = result.Success,
                    ["result"] = result.Success && result.Result != null ? new JValue(result.Result.Value) : JValue.CreateNull(),
                    ["error"] = result.Success ? JValue.CreateNull() : new JValue(result.ErrorMessage),
                    ["rawResponse"] = result.Response != null ? JToken.FromObject(result.Response) : JValue.CreateNull(),
                };
                return responseObj;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageTools] Error in GenerateImageToolWrapper: {ex.Message}");
                // Return error object as JObject
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Error: {ex.Message}",
                };
            }
        }
    }
}
