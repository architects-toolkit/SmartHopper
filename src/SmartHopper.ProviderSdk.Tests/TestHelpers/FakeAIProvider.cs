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

namespace SmartHopper.ProviderSdk.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.AIProviders;

    /// <summary>
    /// Minimal in-memory AI provider used by SDK contract tests.
    /// Produces deterministic request bodies and decodes deterministic responses.
    /// </summary>
    public sealed class FakeAIProvider : AIProvider
    {
        /// <summary>
        /// The canonical provider name used in tests.
        /// </summary>
        public const string ProviderName = "FakeProvider";

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeAIProvider"/> class.
        /// </summary>
        public FakeAIProvider()
        {
            this.Models = new FakeProviderModels();
        }

        /// <inheritdoc />
        public override string Name => ProviderName;

        /// <inheritdoc />
        public override Image Icon => new Bitmap(1, 1);

        /// <inheritdoc />
        public override bool IsEnabled => true;

        /// <inheritdoc />
        public override bool IsConfigured => !string.IsNullOrEmpty(this.GetApiKey());

        /// <inheritdoc />
        public override Uri DefaultServerUrl => new Uri("https://fake.example/");

        /// <inheritdoc />
        public override string Encode(AIRequestCall request)
        {
            var body = new JObject
            {
                ["model"] = request?.Model,
                ["messages"] = new JArray(
                    request?.Body?.Interactions?.Select(this.EncodeInteraction) ?? Enumerable.Empty<JToken>()),
            };

            return body.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <inheritdoc />
        public override string Encode(IAIInteraction interaction)
        {
            return this.EncodeInteraction(interaction).ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <inheritdoc />
        public override List<IAIInteraction> Decode(JObject response)
        {
            var interactions = new List<IAIInteraction>();
            if (response == null)
            {
                return interactions;
            }

            var choices = response["choices"] as JArray;
            var choice = choices?.FirstOrDefault() as JObject;
            var message = choice?["message"] as JObject;
            if (message == null)
            {
                return interactions;
            }

            var role = message["role"]?.ToString() ?? "assistant";
            var content = message["content"]?.ToString() ?? string.Empty;
            var agent = role.ToLowerInvariant() switch
            {
                "system" => AIAgent.System,
                "user" => AIAgent.User,
                "assistant" => AIAgent.Assistant,
                _ => AIAgent.Assistant,
            };

            if (!string.IsNullOrEmpty(content))
            {
                interactions.Add(new AIInteractionText
                {
                    Agent = agent,
                    Content = content,
                    Metrics = new AIMetrics { FinishReason = "stop" },
                });
            }

            var toolCalls = message["tool_calls"] as JArray;
            if (toolCalls != null)
            {
                foreach (var tc in toolCalls.OfType<JObject>())
                {
                    var function = tc["function"] as JObject;
                    var name = function?["name"]?.ToString() ?? string.Empty;
                    var arguments = function?["arguments"];
                    JObject? argsObj = null;
                    if (arguments != null)
                    {
                        try
                        {
                            argsObj = arguments is JObject jo ? jo : JObject.Parse(arguments.ToString());
                        }
                        catch
                        {
                            argsObj = new JObject();
                        }
                    }

                    interactions.Add(new AIInteractionToolCall
                    {
                        Agent = AIAgent.ToolCall,
                        Id = tc["id"]?.ToString() ?? string.Empty,
                        Name = name,
                        Arguments = argsObj ?? new JObject(),
                        Metrics = new AIMetrics { FinishReason = "tool" },
                    });
                }
            }

            return interactions;
        }

        /// <summary>
        /// Configures the provider with an API key and an optional model.
        /// </summary>
        /// <param name="apiKey">The API key to use.</param>
        /// <param name="model">The model name to use.</param>
        public void Configure(string apiKey, string model = "fake-model")
        {
            this.RefreshCachedSettings(new Dictionary<string, object>
            {
                { "ApiKey", apiKey },
                { "Model", model },
            });
        }

        private JObject EncodeInteraction(IAIInteraction interaction)
        {
            var obj = new JObject
            {
                ["role"] = interaction?.Agent switch
                {
                    AIAgent.System => "system",
                    AIAgent.User => "user",
                    AIAgent.Assistant => "assistant",
                    _ => "assistant",
                },
            };

            if (interaction is AIInteractionText text && !string.IsNullOrEmpty(text.Content))
            {
                obj["content"] = text.Content;
            }

            if (interaction is AIInteractionToolCall toolCall)
            {
                obj["content"] = $"tool_call:{toolCall.Name}";
                obj["tool_call"] = new JObject
                {
                    ["id"] = toolCall.Id,
                    ["name"] = toolCall.Name,
                    ["arguments"] = toolCall.Arguments?.ToString() ?? string.Empty,
                };
            }

            return obj;
        }
    }
}
