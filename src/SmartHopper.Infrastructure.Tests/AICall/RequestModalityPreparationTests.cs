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

namespace SmartHopper.Infrastructure.Tests.AICall
{
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

    /// <summary>
    /// Deterministic tests for <see cref="RequestModalityPreparationService"/> using a fake
    /// provider registry and a fake vision/speech helper — no user settings, no real
    /// ProviderManager, no network.
    /// </summary>
    [Collection("ProviderSdk")]
    public class RequestModalityPreparationTests : IDisposable
    {
        private const string Primary = "primaryprov";
        private const string PrimaryModel = "textmodel";
        private const string Helper = "helperprov";
        private const string HelperModel = "visionmodel";
        private const string Speech = "speechprov";
        private const string SpeechModel = "speechmodel";

        private readonly Dictionary<string, AIModelCapabilities> previousModels;
        private readonly IProviderRegistryHost previousRegistry;

        private readonly FakeProvider helperProvider = new FakeProvider(Helper);
        private readonly FakeProvider speechProvider = new FakeProvider(Speech);
        private readonly FakeProvider primaryProvider = new FakeProvider(Primary);

        public RequestModalityPreparationTests()
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
            registry.Models[$"{Helper}.{HelperModel}"] = new AIModelCapabilities
            {
                Provider = Helper,
                Model = HelperModel,
                Capabilities = AICapability.Text2Text | AICapability.Image2Text,
            };
            registry.Models[$"{Speech}.{SpeechModel}"] = new AIModelCapabilities
            {
                Provider = Speech,
                Model = SpeechModel,
                Capabilities = AICapability.Speech2Text,
            };

            this.previousRegistry = ProviderSdkHost.ProviderRegistry;
            ProviderSdkHost.ProviderRegistry = new FakeRegistry(
                (Primary, this.primaryProvider),
                (Helper, this.helperProvider),
                (Speech, this.speechProvider));
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

        private static SourceContentReference PageSource(string doc = "doc-1", int page = 1, string caption = "Figure 2: loss curve")
            => new SourceContentReference
            {
                DocumentId = doc,
                SourceName = "paper.pdf",
                PageNumber = page,
                FigureLabel = "Figure 2",
                Kind = SourceContentKind.Page,
                Caption = caption,
                ContextText = "The loss curve plateaus after epoch 40.",
            };

        private AIRequestCall MixedRequest(string provider = Primary, string model = PrimaryModel)
        {
            var interactions = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.System, Content = "UNRELATED-INSTRUCTION-12345" },
                new AIInteractionText
                {
                    Agent = AIAgent.User,
                    TurnId = "turn-1",
                    Content = "Summarize this paper.",
                    SourceReference = PageSource(),
                },
                new AIInteractionImage
                {
                    Agent = AIAgent.User,
                    TurnId = "turn-1",
                    ImageData = "aW1hZ2VkYXRh",
                    MimeType = "image/png",
                    SourceReference = PageSource(),
                },
            };

            var request = new AIRequestCall();
            request.Initialize(provider, model, interactions, origin: "test", capability: AICapability.Text2Text);
            return request;
        }

        private static Func<string, string, AICapability, ModalityFallbackMode, FallbackChain> ImageChain(
            string helper = Helper, string model = HelperModel)
            => (provider, requestedModel, required, mode) => new FallbackChain(
                new List<FallbackStep> { new FallbackStep(new ImageToTextFallback(), helper, model) },
                "image to text",
                AICapability.TextInput);

#if NET7_WINDOWS
        [Fact(DisplayName = "Text-only primary converts image via helper preserving source and TurnId [Windows]")]
#else
        [Fact(DisplayName = "Text-only primary converts image via helper preserving source and TurnId [Core]")]
#endif
        public async Task TextOnlyPrimary_ConvertsImage_PreservesProvenance()
        {
            var request = this.MixedRequest();
            var canonicalBody = request.Body;

            var preparation = await RequestModalityPreparationService.PrepareAsync(
                request,
                ModalityFallbackMode.ConfiguredProvider,
                ImageChain(),
                CancellationToken.None);

            Assert.False(preparation.HasBlockingErrors, string.Join(" | ", preparation.Messages.Select(m => $"{m.Severity}:{m.Message}")));
            Assert.Equal(Primary, preparation.Request.Provider);
            Assert.Equal(PrimaryModel, preparation.Request.Model);
            Assert.Same(canonicalBody, request.Body);
            Assert.Contains(request.Body.Interactions, i => i is AIInteractionImage);

            var helperRequest = this.helperProvider.LastRequest;
            Assert.NotNull(helperRequest);
            var systemText = helperRequest.Body.Interactions.OfType<AIInteractionText>().First(t => t.Agent == AIAgent.System);
            Assert.Equal(VisualEvidencePrompts.ContextualEvidence, systemText.Content);
            var helperImage = helperRequest.Body.Interactions.OfType<AIInteractionImage>().Single();
            Assert.Equal(AIAgent.User, helperImage.Agent);

            var helperUserText = helperRequest.Body.Interactions.OfType<AIInteractionText>().Last(t => t.Agent == AIAgent.User).Content;
            Assert.Contains("paper.pdf", helperUserText);
            Assert.Contains("Figure 2: loss curve", helperUserText);
            Assert.Contains("Summarize this paper.", helperUserText);
            Assert.DoesNotContain("UNRELATED-INSTRUCTION-12345", helperUserText);

            var evidence = preparation.Request.Body.Interactions.OfType<AIInteractionText>()
                .FirstOrDefault(t => t.SourceReference?.DocumentId == "doc-1" && t.TurnId == "turn-1" && t != request.Body.Interactions[1]);
            Assert.NotNull(evidence);
            Assert.Equal(FakeProvider.Evidence, evidence.Content);
            Assert.Equal(AIAgent.User, evidence.Agent);
            Assert.Equal("doc-1", evidence.SourceReference.DocumentId);
            Assert.Single(preparation.ExtraMetrics);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Model-visible tool image becomes stable evidence array keeping tool correlation [Windows]")]
#else
        [Fact(DisplayName = "Model-visible tool image becomes stable evidence array keeping tool correlation [Core]")]
#endif
        public async Task ToolResultImage_ConvertsToEvidenceArray_PreservesCorrelation()
        {
            var toolResult = new AIInteractionToolResult
            {
                Id = "call-9",
                Name = "inspect_doc",
                TurnId = "turn-2",
                Result = new JObject { ["ok"] = true, ["mimeType"] = "image/png" },
                Images = new List<ToolResultImage>
                {
                    new ToolResultImage
                    {
                        ImageData = "dG9vbGltZw==",
                        MimeType = "image/png",
                        SendToModel = true,
                        SourceReference = PageSource(),
                    },
                    new ToolResultImage { ImageData = "ZGlzcGxheQ==", MimeType = "image/png", SendToModel = false },
                },
            };

            var request = new AIRequestCall();
            request.Initialize(
                Primary,
                PrimaryModel,
                new List<IAIInteraction>
                {
                    new AIInteractionToolCall { Id = "call-9", Name = "inspect_doc", Arguments = new JObject { ["q"] = 1 }, TurnId = "turn-2" },
                    toolResult,
                },
                origin: "test",
                capability: AICapability.Text2Text);

            var preparation = await RequestModalityPreparationService.PrepareAsync(
                request,
                ModalityFallbackMode.ConfiguredProvider,
                ImageChain(),
                CancellationToken.None);

            Assert.False(preparation.HasBlockingErrors);
            Assert.Equal(1, this.helperProvider.CallCount);

            var outbound = preparation.Request.Body.Interactions.OfType<AIInteractionToolResult>().Single();
            Assert.Equal("call-9", outbound.Id);
            Assert.Equal("inspect_doc", outbound.Name);
            Assert.Equal("turn-2", outbound.TurnId);

            var evidence = outbound.Result["imageEvidence"] as JArray;
            Assert.NotNull(evidence);
            var entry = Assert.Single(evidence) as JObject;
            Assert.NotNull(entry["evidence"]);
            Assert.Equal("doc-1", entry["source"]?["documentId"]?.Value<string>());
            Assert.Equal(1, entry["source"]?["pageNumber"]?.Value<int>());
            Assert.Equal("Figure 2", entry["source"]?["figureLabel"]?.Value<string>());

            var converted = outbound.Images.Single(i => i.ImageData == "dG9vbGltZw==");
            Assert.False(converted.SendToModel);
            var displayOnly = outbound.Images.Single(i => i.ImageData == "ZGlzcGxheQ==");
            Assert.False(displayOnly.SendToModel);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Registered vision target retains media and calls no helper [Windows]")]
#else
        [Fact(DisplayName = "Registered vision target retains media and calls no helper [Core]")]
#endif
        public async Task VisionPrimary_RetainsMedia_NoHelper()
        {
            var registry = AIModelCapabilityRegistry.Instance;
            registry.Models[$"{Primary}.visionprimary"] = new AIModelCapabilities
            {
                Provider = Primary,
                Model = "visionprimary",
                Capabilities = AICapability.Text2Text | AICapability.ImageInput | AICapability.TextOutput,
            };

            var request = this.MixedRequest(model: "visionprimary");
            var canonicalBody = request.Body;

            var preparation = await RequestModalityPreparationService.PrepareAsync(
                request,
                ModalityFallbackMode.ConfiguredProvider,
                ImageChain(),
                CancellationToken.None);

            Assert.False(preparation.HasBlockingErrors);
            Assert.Equal(0, this.helperProvider.CallCount);
            Assert.Same(canonicalBody, preparation.Request.Body);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Disabled fallback, unknown capabilities and missing helper all block [Windows]")]
#else
        [Fact(DisplayName = "Disabled fallback, unknown capabilities and missing helper all block [Core]")]
#endif
        public async Task DisabledUnknownAndMissingHelper_AllBlock()
        {
            var disabled = await RequestModalityPreparationService.PrepareAsync(
                this.MixedRequest(), ModalityFallbackMode.Disabled, ImageChain(), CancellationToken.None);
            Assert.True(disabled.HasBlockingErrors);
            Assert.Equal(0, this.helperProvider.CallCount);

            // Unknown capability is not confirmed support: with fallback disabled the image
            // must not be submitted as an image — the request blocks.
            var unknownDisabled = await RequestModalityPreparationService.PrepareAsync(
                this.MixedRequest(provider: "unregistered", model: "ghost"), ModalityFallbackMode.Disabled, ImageChain(), CancellationToken.None);
            Assert.True(unknownDisabled.HasBlockingErrors);
            Assert.Equal(0, this.helperProvider.CallCount);

            // With a resolvable chain, the same unknown-capability request is converted:
            // incompatible media still never reaches the provider.
            var before = this.helperProvider.CallCount;
            var unknownConverted = await RequestModalityPreparationService.PrepareAsync(
                this.MixedRequest(provider: "unregistered", model: "ghost"), ModalityFallbackMode.AnyProvider, ImageChain(), CancellationToken.None);
            Assert.False(unknownConverted.HasBlockingErrors);
            Assert.Equal(before + 1, this.helperProvider.CallCount);
            Assert.DoesNotContain(unknownConverted.Request.Body.Interactions, i => i is AIInteractionImage);

            var missingHelper = await RequestModalityPreparationService.PrepareAsync(
                this.MixedRequest(),
                ModalityFallbackMode.AnyProvider,
                (p, m, r, mode) => null,
                CancellationToken.None);
            Assert.True(missingHelper.HasBlockingErrors);
            Assert.Equal(before + 1, this.helperProvider.CallCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Helper failure blocks with provider cause and still meters the charged call [Windows]")]
#else
        [Fact(DisplayName = "Helper failure blocks with provider cause and still meters the charged call [Core]")]
#endif
        public async Task HelperFailure_BlocksWithCause_StillMeters()
        {
            this.helperProvider.FailWithMessage = "vision endpoint exploded";

            var preparation = await RequestModalityPreparationService.PrepareAsync(
                this.MixedRequest(), ModalityFallbackMode.ConfiguredProvider, ImageChain(), CancellationToken.None);

            Assert.True(preparation.HasBlockingErrors);
            var error = preparation.Messages.First(m => m.Severity == SHRuntimeMessageSeverity.Error);
            Assert.Contains("vision endpoint exploded", error.Message);
            Assert.Single(preparation.ExtraMetrics);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Repeated same evidence reuses conversion once; differing content does not collide [Windows]")]
#else
        [Fact(DisplayName = "Repeated same evidence reuses conversion once; differing content does not collide [Core]")]
#endif
        public async Task Memoization_SameEvidenceOnce_DifferentContentNoCollision()
        {
            var image = new AIInteractionImage
            {
                Agent = AIAgent.User,
                TurnId = "turn-1",
                ImageData = "c2FtZWJ5dGVz",
                MimeType = "image/png",
                SourceReference = PageSource(),
            };
            var twin = new AIInteractionImage
            {
                Agent = AIAgent.User,
                TurnId = "turn-1",
                ImageData = "c2FtZWJ5dGVz",
                MimeType = "image/png",
                SourceReference = PageSource(),
            };

            var request = new AIRequestCall();
            request.Initialize(Primary, PrimaryModel, new List<IAIInteraction> { image, twin }, origin: "test", capability: AICapability.Text2Text);

            var first = await RequestModalityPreparationService.PrepareAsync(
                request, ModalityFallbackMode.ConfiguredProvider, ImageChain(), CancellationToken.None);
            Assert.False(first.HasBlockingErrors);
            Assert.Equal(1, this.helperProvider.CallCount);
            Assert.Single(first.ExtraMetrics);

            var second = await RequestModalityPreparationService.PrepareAsync(
                request, ModalityFallbackMode.ConfiguredProvider, ImageChain(), CancellationToken.None);
            Assert.False(second.HasBlockingErrors);
            Assert.Equal(1, this.helperProvider.CallCount);
            Assert.Empty(second.ExtraMetrics);

            var other = new AIInteractionImage
            {
                Agent = AIAgent.User,
                ImageData = "ZGlmZmVyZW50",
                MimeType = "image/jpeg",
                SourceReference = PageSource(),
            };
            var request2 = new AIRequestCall();
            request2.Initialize(Primary, PrimaryModel, new List<IAIInteraction> { other }, origin: "test", capability: AICapability.Text2Text);
            request2.PreparationCache = request.PreparationCache;

            var third = await RequestModalityPreparationService.PrepareAsync(
                request2, ModalityFallbackMode.ConfiguredProvider, ImageChain(), CancellationToken.None);
            Assert.False(third.HasBlockingErrors);
            Assert.Equal(2, this.helperProvider.CallCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Dedicated speech helper transcribes audio without recursing on AudioInput [Windows]")]
#else
        [Fact(DisplayName = "Dedicated speech helper transcribes audio without recursing on AudioInput [Core]")]
#endif
        public async Task SpeechHelper_Transcribes_NoRecursion()
        {
            var audio = new AIInteractionAudio
            {
                Agent = AIAgent.User,
                TurnId = "turn-5",
                Data = new byte[] { 1, 2, 3, 4 },
                MimeType = "audio/mp3",
            };
            var request = new AIRequestCall();
            request.Initialize(Primary, PrimaryModel, new List<IAIInteraction> { audio }, origin: "test", capability: AICapability.Text2Text);

            var preparation = await RequestModalityPreparationService.PrepareAsync(
                request,
                ModalityFallbackMode.AnyProvider,
                (p, m, r, mode) => new FallbackChain(
                    new List<FallbackStep> { new FallbackStep(new AudioToTextFallback(), Speech, SpeechModel) },
                    "audio to text",
                    AICapability.TextInput),
                CancellationToken.None);

            Assert.False(preparation.HasBlockingErrors);
            Assert.Equal(1, this.speechProvider.CallCount);

            var transcript = preparation.Request.Body.Interactions.OfType<AIInteractionText>().Single();
            Assert.Equal("turn-5", transcript.TurnId);
            Assert.Equal(AIAgent.User, transcript.Agent);
            Assert.Contains(FakeProvider.Evidence, transcript.Content);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Cancellation before preparation aborts before any helper call [Windows]")]
#else
        [Fact(DisplayName = "Cancellation before preparation aborts before any helper call [Core]")]
#endif
        public async Task Cancellation_AbortsPreparation()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                RequestModalityPreparationService.PrepareAsync(
                    this.MixedRequest(), ModalityFallbackMode.ConfiguredProvider, ImageChain(), cts.Token));
            Assert.Equal(0, this.helperProvider.CallCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Cancellation on second image keeps paid helper metrics once [Windows]")]
#else
        [Fact(DisplayName = "Cancellation on second image keeps paid helper metrics once [Core]")]
#endif
        public async Task SecondImageCancellation_KeepsPaidMetrics_StillBlocks()
        {
            var cts = new CancellationTokenSource();
            this.helperProvider.OnCall = _ => cts.Cancel();

            var request = new AIRequestCall();
            request.Initialize(
                Primary,
                PrimaryModel,
                new List<IAIInteraction>
                {
                    new AIInteractionImage { Agent = AIAgent.User, ImageData = "aW1nLTE=", MimeType = "image/png", SourceReference = PageSource() },
                    new AIInteractionImage { Agent = AIAgent.User, ImageData = "aW1nLTI=", MimeType = "image/png", SourceReference = PageSource() },
                },
                origin: "test",
                capability: AICapability.Text2Text);

            var preparation = await RequestModalityPreparationService.PrepareAsync(
                request, ModalityFallbackMode.ConfiguredProvider, ImageChain(), cts.Token);

            // First helper call was charged exactly once; cancellation blocks submission
            // but never discards the paid metric.
            Assert.Equal(1, this.helperProvider.CallCount);
            Assert.True(preparation.HasBlockingErrors);
            Assert.Single(preparation.ExtraMetrics);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Cancellation after helper returned aborts submission and keeps metrics [Windows]")]
#else
        [Fact(DisplayName = "Cancellation after helper returned aborts submission and keeps metrics [Core]")]
#endif
        public async Task AfterHelperCancellation_BlocksAndMeters()
        {
            // Cancel fires while the helper call runs, so the helper still returns success
            // (charged), and the next cancellation checkpoint aborts submission.
            var cts = new CancellationTokenSource();
            this.helperProvider.OnCall = _ => cts.Cancel();

            var request = this.MixedRequest();
            var preparation = await RequestModalityPreparationService.PrepareAsync(
                request, ModalityFallbackMode.ConfiguredProvider, ImageChain(), cts.Token);

            Assert.Equal(1, this.helperProvider.CallCount);
            Assert.True(preparation.HasBlockingErrors);
            Assert.Single(preparation.ExtraMetrics);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Empty helper evidence blocks while still metering the call [Windows]")]
#else
        [Fact(DisplayName = "Empty helper evidence blocks while still metering the call [Core]")]
#endif
        public async Task EmptyHelperResult_BlocksAndMeters()
        {
            this.helperProvider.EmptyResult = true;

            var preparation = await RequestModalityPreparationService.PrepareAsync(
                this.MixedRequest(), ModalityFallbackMode.ConfiguredProvider, ImageChain(), CancellationToken.None);

            Assert.True(preparation.HasBlockingErrors);
            Assert.Contains(preparation.Messages, m => m.Message.Contains("no textual evidence"));
            Assert.Single(preparation.ExtraMetrics);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Invalid explicit helper pin rejects the chain [Windows]")]
#else
        [Fact(DisplayName = "Invalid explicit helper pin rejects the chain [Core]")]
#endif
        public async Task InvalidPinnedModel_Blocks()
        {
            var testSettings = new SmartHopperSettings
            {
                ModalityFallback = ModalityFallbackMode.AnyProvider,
                FallbackProviderPins = new Dictionary<string, FallbackProviderPin>
                {
                    ["ImageToText"] = new FallbackProviderPin { Provider = Helper, Model = "nonexistent-model" },
                },
            };

            var preparation = await RequestModalityPreparationService.PrepareAsync(
                this.MixedRequest(),
                ModalityFallbackMode.AnyProvider,
                (p, m, r, mode) => new ModalityFallbackResolver().Resolve(p, m, r, mode, testSettings),
                CancellationToken.None);

            Assert.True(preparation.HasBlockingErrors);
            Assert.Equal(0, this.helperProvider.CallCount);
        }

        /// <summary>Minimal registry host mapping names to fake providers.</summary>
        /// <summary>
        /// Pipeline stub that returns a prepared execution copy carrying one supplied
        /// helper metric, without running the real modality fallback chain.
        /// </summary>
        private sealed class FakePipeline : IPolicyPipelineHost
        {
            private readonly AIMetrics metric;

            internal FakePipeline(AIMetrics metric) => this.metric = metric;

            public Task ApplyRequestPoliciesAsync(AIRequestCall request) => Task.CompletedTask;

            public Task ApplyResponsePoliciesAsync(AIReturn response) => Task.CompletedTask;

            public Task<AIRequestPreparation> PrepareExecutionAsync(AIRequestCall request, CancellationToken cancellationToken = default)
            {
                var preparation = new AIRequestPreparation(request.CreateExecutionCopy());
                preparation.ExtraMetrics.Add(this.metric);
                return Task.FromResult(preparation);
            }
        }

        /// <summary>
        /// A primary provider that violates the contract by returning null still surfaces
        /// the paid helper-call metrics produced during preparation, exactly once.
        /// </summary>
        [Fact]
        public async Task Exec_NullPrimaryResult_PreservesPreparationExtras()
        {
            var metric = new AIMetrics
            {
                FinishReason = "stop",
                Provider = Helper,
                Model = HelperModel,
                InputTokensPrompt = 7,
                OutputTokensGeneration = 3,
            };
            var previousPipeline = ProviderSdkHost.PolicyPipeline;
            this.primaryProvider.ReturnNullResult = true;
            ProviderSdkHost.PolicyPipeline = new FakePipeline(metric);
            try
            {
                var request = new AIRequestCall { Provider = Primary, Model = PrimaryModel };
                request.Body = AIBody.Empty.WithAppended(new AIInteractionText { Agent = AIAgent.User, Content = "hi" });

                var result = await request.Exec();

                var merged = Assert.Single(result.ExtraMetrics);
                Assert.Equal(Helper, merged.Provider);
                Assert.Equal(HelperModel, merged.Model);
                Assert.Equal(7, merged.InputTokensPrompt);
                Assert.Equal(3, merged.OutputTokensGeneration);
                Assert.Contains(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error);
            }
            finally
            {
                ProviderSdkHost.PolicyPipeline = previousPipeline;
            }
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
        }

        /// <summary>
        /// Deterministic helper provider: records the last request for assertions, returns a
        /// fixed evidence text, or fails with the configured message. Never touches network.
        /// </summary>
        private sealed class FakeProvider : IAIProvider
        {
            public const string Evidence = "EVIDENCE-TEXT-0001";

            public FakeProvider(string name) => this.Name = name;

            public string FailWithMessage { get; set; }

            /// <summary>When true, Call returns a null IAIReturn (provider-contract violation).</summary>
            public bool ReturnNullResult { get; set; }

            /// <summary>When set, the fake returns a successful call containing no usable text.</summary>
            public bool EmptyResult { get; set; }

            /// <summary>Optional hook invoked inside Call (after the count increment) so
            /// tests can cancel a token or inspect the request mid-helper.</summary>
            public Action<AIRequestCall> OnCall { get; set; }

            public int CallCount { get; private set; }

            public AIRequestCall LastRequest { get; private set; }

            public string Name { get; }

            public System.Drawing.Image Icon => null;

            public bool IsEnabled => true;

            public bool IsConfigured => true;

            public string AuthenticationErrorMessage => null;

            public SmartHopper.ProviderSdk.AIModels.IAIProviderModels Models => null;

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
                this.OnCall?.Invoke(request);

                var result = new AIReturn();
                if (this.ReturnNullResult)
                {
                    return Task.FromResult<IAIReturn>(null);
                }

                if (!string.IsNullOrEmpty(this.FailWithMessage))
                {
                    result.CreateProviderError(this.FailWithMessage, request);
                    return Task.FromResult<IAIReturn>(result);
                }

                var body = AIBodyBuilder.Create()
                    .Add(new AIInteractionText
                    {
                        Agent = AIAgent.Assistant,
                        Content = this.EmptyResult ? " " : Evidence,
                        Metrics = new AIMetrics
                        {
                            FinishReason = "stop",
                            Provider = this.Name,
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
