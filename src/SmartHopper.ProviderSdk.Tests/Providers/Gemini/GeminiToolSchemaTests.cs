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

#if NET7_WINDOWS
namespace SmartHopper.ProviderSdk.Tests.Providers.Gemini
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.Gemini;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Hosting;
    using Xunit;

    /// <summary>
    /// Gemini tool-declaration encoding (<c>parametersJsonSchema</c>) and opaque
    /// <c>thoughtSignature</c> preservation across decode → history → re-encode.
    /// </summary>
    [Collection("ProviderSdk")]
    public class GeminiToolSchemaTests : IDisposable
    {
        private readonly IToolRegistryHost previousToolRegistry = ProviderSdkHost.ToolRegistry;
        private readonly IProviderRegistryHost previousProviderRegistry = ProviderSdkHost.ProviderRegistry;
        private readonly ToolRegistry tools = new ToolRegistry();

        public GeminiToolSchemaTests()
        {
            ProviderSdkHost.ToolRegistry = this.tools;
            ProviderSdkHost.ProviderRegistry = new SingleProviderRegistry(GeminiProvider.Instance);
        }

        public void Dispose()
        {
            ProviderSdkHost.ToolRegistry = this.previousToolRegistry;
            ProviderSdkHost.ProviderRegistry = this.previousProviderRegistry;
        }

        /// <summary>
        /// Canonical JSON Schema allows shapes Gemini's restricted <c>parameters</c>
        /// proto rejects (e.g. array-valued <c>type</c>). The wire format must emit
        /// the schema under <c>parametersJsonSchema</c> instead — never both fields.
        /// Reproduces the live HTTP 400 on <c>function_declarations[7]</c>.
        /// </summary>
        [Fact]
        public void Encode_ToolWithUnionType_EmitsParametersJsonSchemaOnly()
        {
            this.tools.Add("gh_connect", "Canvas.Modifications",
                "{\"type\":\"object\",\"properties\":{\"depth\":{\"type\":[\"integer\",\"string\"]}},\"required\":[\"depth\"]}");

            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: "gemini-3.8-flash",
                body: AIBodyBuilder.Create()
                    .WithToolFilter("*")
                    .Add(new AIInteractionText { Agent = AIAgent.User, Content = "wire it" })
                    .Build(),
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling);

            var body = JObject.Parse(GeminiProvider.Instance.Encode(request));

            var declaration = body["tools"]?[0]?["functionDeclarations"]?[0] as JObject;
            Assert.NotNull(declaration);
            Assert.Null(declaration["parameters"]);
            var schema = declaration["parametersJsonSchema"] as JObject;
            Assert.NotNull(schema);
            Assert.Equal("object", schema["type"]?.ToString());
            Assert.Equal(JTokenType.Array, schema["properties"]?["depth"]?["type"]?.Type);
        }

        /// <summary>A schema missing the root type gets <c>type: object</c> — Gemini requires it.</summary>
        [Fact]
        public void Encode_ToolSchemaWithoutRootType_GetsObjectType()
        {
            this.tools.Add("gh_get", "Canvas.Query", "{\"properties\":{\"x\":{\"type\":\"integer\"}}}");

            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: "gemini-3.8-flash",
                body: AIBodyBuilder.Create()
                    .WithToolFilter("*")
                    .Add(new AIInteractionText { Agent = AIAgent.User, Content = "list" })
                    .Build(),
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling);

            var body = JObject.Parse(GeminiProvider.Instance.Encode(request));
            var schema = body["tools"]?[0]?["functionDeclarations"]?[0]?["parametersJsonSchema"] as JObject;

            Assert.Equal("object", schema?["type"]?.ToString());
        }

        /// <summary>
        /// A part-level <c>thoughtSignature</c> returned on a functionCall must land in
        /// the decoded interaction's <see cref="IAIInteraction.ProviderData"/> so it can
        /// be re-emitted verbatim on continuation turns.
        /// </summary>
        [Fact]
        public void Decode_FunctionCall_CapturesThoughtSignature()
        {
            var response = JObject.Parse(@"{
                ""candidates"": [{
                    ""content"": {
                        ""role"": ""model"",
                        ""parts"": [{
                            ""functionCall"": { ""name"": ""gh_get"", ""args"": { ""a"": 1 } },
                            ""thoughtSignature"": ""sig-abc""
                        }]
                    }
                }]
            }");

            var interactions = GeminiProvider.Instance.Decode(response);

            var toolCall = Assert.IsType<AIInteractionToolCall>(Assert.Single(interactions));
            Assert.Equal("sig-abc", toolCall.ProviderData?["thoughtSignature"]?.ToString());
        }

        /// <summary>
        /// The signature stored in <see cref="IAIInteraction.ProviderData"/> must be
        /// re-emitted on the model part when the conversation history is re-encoded —
        /// Gemini rejects continuations missing it.
        /// </summary>
        [Fact]
        public void Encode_ToolCallInteraction_ReemitsThoughtSignature()
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "Gemini",
                model: "gemini-3.8-flash",
                body: AIBodyBuilder.Create()
                    .Add(new AIInteractionToolCall
                    {
                        Agent = AIAgent.Assistant,
                        Id = "call-1",
                        Name = "gh_get",
                        Arguments = new JObject { ["a"] = 1 },
                        ProviderData = new JObject { ["thoughtSignature"] = "sig-abc" },
                    })
                    .Build(),
                origin: string.Empty,
                capability: AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling);

            var body = JObject.Parse(GeminiProvider.Instance.Encode(request));

            var parts = body["contents"]?[0]?["parts"] as JArray;
            Assert.NotNull(parts);
            Assert.Equal("sig-abc", parts[0]?["thoughtSignature"]?.ToString());
            Assert.NotNull(parts[0]?["functionCall"]);
        }

        /// <summary>Registry that serves hand-built tool definitions.</summary>
        private sealed class ToolRegistry : IToolRegistryHost
        {
            public Dictionary<string, ProviderToolDefinition> Tools { get; } = new Dictionary<string, ProviderToolDefinition>();

            public void Add(string name, string category, string schema)
            {
                this.Tools.Add(name, new ProviderToolDefinition
                {
                    Name = name,
                    Category = category,
                    Description = name,
                    ParametersSchema = schema,
                    Surfaces = AIToolSurface.All,
                });
            }

            public void DiscoverTools()
            {
            }

            public IReadOnlyDictionary<string, ProviderToolDefinition> GetTools() => this.Tools;
        }

        /// <summary>Registry that resolves every provider lookup to Gemini.</summary>
        private sealed class SingleProviderRegistry : IProviderRegistryHost
        {
            private readonly AIProvider provider;

            public SingleProviderRegistry(AIProvider provider)
            {
                this.provider = provider;
            }

            public IAIProvider GetProvider(string providerName) => this.provider;

            public IAIProviderSettings GetProviderSettings(string providerName) =>
                new GeminiProviderSettings(GeminiProvider.Instance);
        }
    }
}
#endif
