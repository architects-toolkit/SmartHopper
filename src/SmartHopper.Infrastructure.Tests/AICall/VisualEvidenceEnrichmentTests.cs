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
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Fallback;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Settings;
using SmartHopper.ProviderSdk.Streaming;
using Xunit;

namespace SmartHopper.Infrastructure.Tests.AICall
{
    /// <summary>
    /// Deterministic tests for <see cref="VisualEvidenceEnrichmentService"/> using the
    /// internal settings-snapshot overload — no persisted settings, disk or network.
    /// </summary>
    [Collection("ProviderSdk")]
    public class VisualEvidenceEnrichmentTests : IDisposable
    {
        private const string Primary = "primary-fake";
        private const string PrimaryModel = "primary-text";
        private const string Helper = "helper-fake";
        private const string VisionModel = "vision-ctx";
        private const string PinnedModel = "vision-pinned";
        private const string OcrModel = "ocr-only";

        private readonly Dictionary<string, AIModelCapabilities> previousModels;
        private readonly IProviderRegistryHost previousRegistry;

        private readonly FakeProvider helperProvider = new FakeProvider(Helper);
        private readonly FakeProvider primaryProvider = new FakeProvider(Primary);

        public VisualEvidenceEnrichmentTests()
        {
            var registry = AIModelCapabilityRegistry.Instance;
            this.previousModels = new Dictionary<string, AIModelCapabilities>(registry.Models);
            registry.Models.Clear();
            registry.Models[$"{Primary}.{PrimaryModel}"] = new AIModelCapabilities
            {
                Provider = Primary,
                Model = PrimaryModel,
                Capabilities = AICapability.Text2Text,
            };
            registry.Models[$"{Helper}.{VisionModel}"] = new AIModelCapabilities
            {
                Provider = Helper,
                Model = VisionModel,
                Capabilities = AICapability.Text2Text | AICapability.Image2Text | AICapability.TextInput,
            };
            registry.Models[$"{Helper}.{PinnedModel}"] = new AIModelCapabilities
            {
                Provider = Helper,
                Model = PinnedModel,
                Capabilities = AICapability.Text2Text | AICapability.Image2Text | AICapability.TextInput,
            };
            registry.Models[$"{Helper}.{OcrModel}"] = new AIModelCapabilities
            {
                Provider = Helper,
                Model = OcrModel,
                Capabilities = AICapability.Image2Text,
            };

            this.previousRegistry = ProviderSdkHost.ProviderRegistry;
            ProviderSdkHost.ProviderRegistry = new FakeRegistry(
                (Primary, this.primaryProvider),
                (Helper, this.helperProvider));
        }

        public void Dispose()
        {
            var registry = AIModelCapabilityRegistry.Instance;
            registry.Models.Clear();
            foreach (var entry in this.previousModels)
            {
                registry.Models[entry.Key] = entry.Value;
            }

            ProviderSdkHost.ProviderRegistry = this.previousRegistry;
        }

        private static AIBody SourceBody(bool displayOnly = false, bool noImages = false)
        {
            var toolResult = new AIInteractionToolResult
            {
                Id = "call-1",
                Name = "file2md",
                Result = new JObject { ["content"] = "# Source markdown" },
                Images = noImages
                    ? new List<ToolResultImage>()
                    : new List<ToolResultImage>
                    {
                        new ToolResultImage
                        {
                            ImageData = "aW1hZ2VkYXRh",
                            MimeType = "image/png",
                            SendToModel = !displayOnly,
                            SourceReference = new SourceContentReference
                            {
                                DocumentId = "doc-1",
                                SourceName = "paper.pdf",
                                PageNumber = 2,
                                AssetId = "img-1",
                                Caption = "Figure 2: loss curve",
                                ContextText = "The loss curve plateaus after epoch 40.",
                                Kind = SourceContentKind.Image,
                            },
                        },
                    },
            };

            return AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Describe this document." })
                .Add(toolResult)
                .Build();
        }

        private static SmartHopperSettings Settings(
            ModalityFallbackMode mode = ModalityFallbackMode.ConfiguredProvider,
            string defaultProvider = Primary,
            Dictionary<string, FallbackProviderPin> pins = null)
            => new SmartHopperSettings
            {
                DefaultAIProvider = defaultProvider,
                ModalityFallback = mode,
                FallbackProviderPins = pins ?? new Dictionary<string, FallbackProviderPin>(),
            };

        [Fact]
        public async Task NoImages_ReturnsUnchangedAndCallsNoProvider()
        {
            var source = SourceBody(noImages: true);
            var result = await VisualEvidenceEnrichmentService.EnrichAsync(
                source, null, null, CancellationToken.None, Settings());

            Assert.Same(source, result.TransformedBody);
            Assert.Equal(0, this.helperProvider.CallCount);
        }

        [Fact]
        public async Task DisplayOnlyImages_ReturnsUnchangedAndCallsNoProvider()
        {
            var source = SourceBody(displayOnly: true);
            var result = await VisualEvidenceEnrichmentService.EnrichAsync(
                source, null, null, CancellationToken.None, Settings());

            Assert.Same(source, result.TransformedBody);
            Assert.Equal(0, this.helperProvider.CallCount);
        }

        [Fact]
        public async Task DisabledMode_StillAllowsExplicitConfiguredVisionConversion()
        {
            var result = await VisualEvidenceEnrichmentService.EnrichAsync(
                SourceBody(), Helper, VisionModel, CancellationToken.None,
                Settings(mode: ModalityFallbackMode.Disabled));

            Assert.Equal(1, this.helperProvider.CallCount);
            Assert.DoesNotContain(result.Messages, m => m?.Severity == SHRuntimeMessageSeverity.Error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public async Task MissingOrWhitespacePreference_UsesSnapshotDefaultProvider(string preferred)
        {
            var result = await VisualEvidenceEnrichmentService.EnrichAsync(
                SourceBody(), preferred, null, CancellationToken.None,
                Settings(defaultProvider: Helper));

            // Default provider resolves the helper through configured-provider mode.
            Assert.Equal(1, this.helperProvider.CallCount);
            Assert.Equal(0, this.primaryProvider.CallCount);
            Assert.Equal(Helper, this.helperProvider.LastRequest?.Provider);
            var model = this.helperProvider.LastRequest?.Model;
            Assert.False(string.IsNullOrWhiteSpace(model));
            Assert.NotEqual(OcrModel, model);
            Assert.DoesNotContain(result.Messages, m => m?.Severity == SHRuntimeMessageSeverity.Error);
            Assert.Contains(
                result.TransformedBody.Interactions.OfType<AIInteractionToolResult>(),
                r => r.Result?["imageEvidence"] is JArray);
        }

        [Fact]
        public async Task ExplicitPin_WinsOverPreferredModel()
        {
            var pins = new Dictionary<string, FallbackProviderPin>
            {
                ["ImageToText"] = new FallbackProviderPin { Provider = Helper, Model = PinnedModel },
            };

            await VisualEvidenceEnrichmentService.EnrichAsync(
                SourceBody(), Helper, VisionModel, CancellationToken.None,
                Settings(pins: pins));

            Assert.Equal(1, this.helperProvider.CallCount);
            Assert.Equal(PinnedModel, this.helperProvider.LastRequest?.Model);
        }

        [Fact]
        public async Task InvalidPin_FailsWithoutSubstitution()
        {
            var pins = new Dictionary<string, FallbackProviderPin>
            {
                ["ImageToText"] = new FallbackProviderPin { Provider = "ghost-provider", Model = "ghost" },
            };

            var result = await VisualEvidenceEnrichmentService.EnrichAsync(
                SourceBody(), Helper, VisionModel, CancellationToken.None,
                Settings(pins: pins));

            Assert.Equal(0, this.helperProvider.CallCount);
            Assert.Contains(result.Messages, m => m?.Severity == SHRuntimeMessageSeverity.Error);
        }

        [Fact]
        public async Task NoImageCapableHelper_FailsExplicit()
        {
            // OCR-only model (Image2Text without TextInput) cannot serve the
            // contextual helper — no eligible conversion exists.
            var pins = new Dictionary<string, FallbackProviderPin>
            {
                ["ImageToText"] = new FallbackProviderPin { Provider = Helper, Model = OcrModel },
            };

            var result = await VisualEvidenceEnrichmentService.EnrichAsync(
                SourceBody(), Helper, OcrModel, CancellationToken.None,
                Settings(pins: pins));

            Assert.Equal(0, this.helperProvider.CallCount);
            Assert.Contains(result.Messages, m => m?.Severity == SHRuntimeMessageSeverity.Error);
        }

        [Fact]
        public async Task FailingHelper_ReturnsErrorAndKeepsItsMetricsOnce()
        {
            this.helperProvider.FailWithMessage = "helper exploded";
            this.helperProvider.FailWithMetrics = true;

            var result = await VisualEvidenceEnrichmentService.EnrichAsync(
                SourceBody(), Helper, VisionModel, CancellationToken.None, Settings());

            Assert.Equal(1, this.helperProvider.CallCount);
            Assert.Contains(result.Messages, m => m?.Severity == SHRuntimeMessageSeverity.Error);

            // Failed helper usage is still metered exactly once with the supplied fields.
            var metric = Assert.Single(result.ExtraMetricsList);
            Assert.Equal(Helper, metric.Provider);
            Assert.Equal(VisionModel, metric.Model);
            Assert.Equal(100, metric.InputTokensPrompt);
            Assert.Equal(50, metric.OutputTokensGeneration);
        }

        [Fact]
        public async Task SuccessfulHelper_MetricsSurfaceOnceAndSourceBodyUnmodified()
        {
            var source = SourceBody();
            var originalImage = ((AIInteractionToolResult)source.Interactions[1]).Images[0].ImageData;

            var result = await VisualEvidenceEnrichmentService.EnrichAsync(
                source, Helper, VisionModel, CancellationToken.None, Settings());

            Assert.Equal(1, this.helperProvider.CallCount);

            // Supplied helper metrics surface exactly once — never derived or duplicated.
            var supplied = result.ExtraMetricsList.Where(m =>
                m != null && m.InputTokensPrompt == 100 && m.OutputTokensGeneration == 50).ToList();
            Assert.Single(supplied);

            // Source body and original image bytes are untouched by enrichment.
            var afterImage = ((AIInteractionToolResult)source.Interactions[1]).Images[0].ImageData;
            Assert.Equal("aW1hZ2VkYXRh", originalImage);
            Assert.Equal(originalImage, afterImage);

            var transformed = result.TransformedBody.Interactions
                .OfType<AIInteractionToolResult>().FirstOrDefault(r => r.Result?["imageEvidence"] is JArray);
            Assert.NotNull(transformed);
        }

        [Fact]
        public async Task HelperRequest_SendsUserImageWithSourceContext_AndSharedMasterPrompt()
        {
            await VisualEvidenceEnrichmentService.EnrichAsync(
                SourceBody(), Helper, VisionModel, CancellationToken.None, Settings());

            var request = this.helperProvider.LastRequest;
            Assert.NotNull(request);

            var interactions = request.Body?.Interactions?.ToList() ?? new List<IAIInteraction>();
            Assert.Contains(interactions, i => i is AIInteractionImage img
                && img.Agent == AIAgent.User
                && img.ImageData == "aW1hZ2VkYXRh");
            Assert.Contains(interactions, i => i is AIInteractionText t
                && t.Agent == AIAgent.User
                && t.Content.Contains("loss curve"));
            Assert.Contains(interactions, i => i is AIInteractionText t
                && t.Content == VisualEvidencePrompts.ContextualEvidence);
        }

        private sealed class FakeRegistry : IProviderRegistryHost
        {
            private readonly Dictionary<string, IAIProvider> providers;

            public FakeRegistry(params (string Name, IAIProvider Provider)[] entries)
            {
                this.providers = entries.ToDictionary(e => e.Name, e => e.Provider, StringComparer.OrdinalIgnoreCase);
            }

            public IAIProvider GetProvider(string providerName)
                => providerName != null && this.providers.TryGetValue(providerName, out var p) ? p : null;

            public IAIProviderSettings GetProviderSettings(string providerName) => null;

            public IEnumerable<IAIProvider> GetProviders() => this.providers.Values;
        }

        /// <summary>Deterministic helper provider — records the request, returns fixed evidence.</summary>
        private sealed class FakeProvider : IAIProvider
        {
            public const string Evidence = "EVIDENCE-TEXT-0001";

            public FakeProvider(string name) => this.Name = name;

            public string FailWithMessage { get; set; }

            /// <summary>When true, the failed return still carries paid helper usage once.</summary>
            public bool FailWithMetrics { get; set; }

            public int CallCount { get; private set; }

            public AIRequestCall LastRequest { get; private set; }

            public string Name { get; }

            public System.Drawing.Image Icon => null;

            public bool IsEnabled => true;

            public bool IsConfigured => true;

            public string AuthenticationErrorMessage => null;

            public IAIProviderModels Models => null;

            public Task InitializeProviderAsync() => Task.CompletedTask;

            public string Encode(AIRequestCall request) => string.Empty;

            public string Encode(IAIInteraction interaction) => string.Empty;

            public string Encode(List<IAIInteraction> interactions) => string.Empty;

            public List<IAIInteraction> Decode(JObject response) => new List<IAIInteraction>();

            public Task<AIRequestCall> PreCallAsync(AIRequestCall request, CancellationToken cancellationToken = default)
                => Task.FromResult(request);

            public Task<IAIReturn> Call(AIRequestCall request, CancellationToken cancellationToken = default)
            {
                this.CallCount++;
                this.LastRequest = request;
                cancellationToken.ThrowIfCancellationRequested();

                var result = new AIReturn();
                if (!string.IsNullOrEmpty(this.FailWithMessage))
                {
                    result.CreateError(
                        this.FailWithMessage, request,
                        this.FailWithMetrics
                            ? new AIMetrics
                            {
                                FinishReason = "error",
                                Provider = this.Name,
                                Model = request.Model,
                                InputTokensPrompt = 100,
                                OutputTokensGeneration = 50,
                            }
                            : null);
                    return Task.FromResult<IAIReturn>(result);
                }

                var body = AIBodyBuilder.Create()
                    .Add(new AIInteractionText
                    {
                        Agent = AIAgent.Assistant,
                        Content = Evidence,
                        Metrics = new AIMetrics
                        {
                            FinishReason = "stop",
                            Provider = this.Name,
                            Model = request.Model,
                            InputTokensPrompt = 100,
                            OutputTokensGeneration = 50,
                        },
                    })
                    .Build();
                result.CreateSuccess(body, request);
                return Task.FromResult<IAIReturn>(result);
            }

            public IAIReturn PostCall(IAIReturn response) => response;

            public string GetDefaultModel(AICapability requiredCapability = AICapability.Text2Text, bool useSettings = true)
                => null;

            public string SelectModel(AICapability requiredCapability, string requestedModel)
                => requestedModel ?? string.Empty;

            public void RefreshCachedSettings(Dictionary<string, object> settings)
            {
            }

            public IEnumerable<SettingDescriptor> GetSettingDescriptors() => Enumerable.Empty<SettingDescriptor>();

            public IStreamingAdapter GetStreamingAdapter() => null;

            public IEnumerable<AIExtraDescriptor> GetExtraDescriptors() => Enumerable.Empty<AIExtraDescriptor>();
        }
    }
}
