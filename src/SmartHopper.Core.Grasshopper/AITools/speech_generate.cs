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
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides a tool for generating speech audio from text.
    /// </summary>
    public class speech_generate : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "speech_generate";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.SpeechOutput;

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generates speech audio from text input",
                category: "Speech",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""text"": {
                            ""type"": ""string"",
                            ""description"": ""The text to convert to speech""
                        },
                        ""voice"": {
                            ""type"": ""string"",
                            ""description"": ""The voice ID to use (e.g., 'alloy', 'echo', 'fable', 'onyx', 'nova', 'shimmer')"",
                            ""default"": ""nova""
                        },
                        ""speed"": {
                            ""type"": ""string"",
                            ""description"": ""Speech speed from 0.25 to 4.0"",
                            ""default"": ""1.0""
                        }
                    },
                    ""required"": [""text""]
                }",
                execute: this.GenerateSpeechToolWrapper,
                requiredCapabilities: this.toolCapabilityRequirements);
        }

        /// <summary>
        /// Builds an <see cref="AIRequestCall"/> from the tool call parameters without executing it.
        /// Used during batch collection to aggregate multiple requests into a single batch submission.
        /// </summary>
        /// <param name="toolCall">The tool call containing provider, model, and arguments.</param>
        /// <returns>A fully-specified <see cref="AIRequestCall"/> ready for batch submission.</returns>
        private AIRequestCall BuildGenerateSpeechRequest(AIToolCall toolCall)
        {
            AIInteractionToolCall toolInfo = toolCall.GetToolCall();
            var args = toolInfo.Arguments ?? new JObject();
            string text = args["text"]?.ToString();

            // Get voice from args, extra settings, or default
            string voice = args["voice"]?.ToString();
            if (string.IsNullOrEmpty(voice))
            {
                voice = toolCall.Parameters != null && toolCall.Parameters.Extras != null && toolCall.Parameters.Extras.TryGetValue("voice", out var v) ? v?.ToString() : null;
            }

            if (string.IsNullOrEmpty(voice))
            {
                voice = SmartHopperSettings.Instance.GetSetting("speech_voice", "nova")?.ToString() ?? "nova";
            }

            // Get speed from args, extra settings, or default
            string speed = args["speed"]?.ToString();
            if (string.IsNullOrEmpty(speed))
            {
                speed = toolCall.Parameters != null && toolCall.Parameters.Extras != null && toolCall.Parameters.Extras.TryGetValue("speed", out var s) ? s?.ToString() : null;
            }

            if (string.IsNullOrEmpty(speed))
            {
                speed = SmartHopperSettings.Instance.GetSetting("speech_speed", "1.0")?.ToString() ?? "1.0";
            }

            var requestBody = AIBodyBuilder.Create()
                .AddUser(text)
                .Build();

            var request = new AIRequestCall();
            request.Initialize(
                provider: toolCall.Provider,
                model: toolCall.Model,
                body: requestBody,
                endpoint: this.toolName,
                capability: this.toolCapabilityRequirements);

            // Build parameters with extras using the builder pattern
            var paramBuilder = AIRequestParameters.Create();
            if (toolCall.Parameters != null && toolCall.Parameters.Extras != null)
            {
                paramBuilder.WithExtras(toolCall.Parameters.Extras);
            }

            paramBuilder.WithExtra("voice", voice);
            paramBuilder.WithExtra("speed", speed);
            request.Parameters = paramBuilder.Build();
            return request;
        }

        /// <summary>
        /// Tool wrapper for the GenerateSpeech function.
        /// </summary>
        /// <param name="toolCall">The tool call information.</param>
        /// <returns>AIReturn with the result.</returns>
        private async Task<AIReturn> GenerateSpeechToolWrapper(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[SpeechTools] Running GenerateSpeech tool");

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                string text = args["text"]?.ToString();

                if (string.IsNullOrEmpty(text))
                {
                    output.CreateToolError("Missing required parameter: text", toolCall);
                    return output;
                }

                var request = this.BuildGenerateSpeechRequest(toolCall);
                var result = await request.Exec().ConfigureAwait(false);

                if (!result.Success)
                {
                    output.Messages = result.Messages;
                    return output;
                }

                var response = result.Body.GetLastInteraction(AIAgent.Assistant)?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(response))
                {
                    if (result.Messages != null)
                    {
                        output.Messages = result.Messages;
                    }

                    output.CreateToolError("Empty response from AI assistant.");
                    return output;
                }

                // Convert response to VersatileAudio
                VersatileAudio audio;
                try
                {
                    audio = VersatileAudio.FromString(response.Trim());
                }
                catch (Exception ex)
                {
                    output.CreateToolError($"Failed to create VersatileAudio from response: {ex.Message}");
                    return output;
                }

                var toolResult = new JObject();
                toolResult.Add("audioPath", response.Trim());
                toolResult.Add("mimeType", audio.MimeType);

                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.toolName,
                        type: ToolResultContentType.Object,
                        payloadPath: "audioPath",
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
                Debug.WriteLine($"[SpeechTools] Error in GenerateSpeech: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
