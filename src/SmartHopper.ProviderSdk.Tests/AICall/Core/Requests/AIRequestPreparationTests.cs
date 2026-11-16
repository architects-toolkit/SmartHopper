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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Requests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Diagnostics;
    using SmartHopper.ProviderSdk.Hosting;
    using Xunit;

    /// <summary>
    /// Tests for model-bound request preparation contracts: execution copies, strict
    /// outbound capability validation and the null host's no-fallback behavior.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIRequestPreparationTests
    {
        private static AIInteractionImage VisionImage(string data = "aW1n")
            => new AIInteractionImage { Agent = AIAgent.User, ImageData = data, MimeType = "image/png" };

        private static AIRequestCall ImageRequest(string provider = "prov", string model = "m")
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider,
                model,
                new List<IAIInteraction> { VisionImage() },
                origin: "test",
                // No capability context: Model resolves to the requested value verbatim,
                // keeping this fixture independent of provider registration.
                capability: AICapability.None);
            return request;
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CreateExecutionCopy detaches mutable fields and retains canonical body [Windows]")]
#else
        [Fact(DisplayName = "CreateExecutionCopy detaches mutable fields and retains canonical body [Core]")]
#endif
        public void CreateExecutionCopy_DetachesMutableRetainsCanonical()
        {
            var request = ImageRequest();
            request.Headers["X-Test"] = "1";
            request.Messages = new List<SHRuntimeMessage>
            {
                new SHRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Validation, SHMessageCode.Unknown, "note"),
            };

            var copy = request.CreateExecutionCopy();

            Assert.NotSame(request, copy);
            Assert.Same(request.Body, copy.Body);
            Assert.Same(request.Parameters, copy.Parameters);
            Assert.Equal("1", copy.Headers["X-Test"]);

            // Detaching: mutating the copy never leaks into the canonical request.
            copy.Headers["X-Test"] = "2";
            copy.Body = AIBody.Empty;
            Assert.Equal("1", request.Headers["X-Test"]);
            Assert.NotSame(copy.Body, request.Body);
            Assert.Equal("test", copy.Origin);
            Assert.Same(request, copy.Source);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "NullPolicyPipelineHost blocks unregistered image capability [Windows]")]
#else
        [Fact(DisplayName = "NullPolicyPipelineHost blocks unregistered image capability [Core]")]
#endif
        public async Task NullHost_UnknownImageCapability_Blocks()
        {
            var preparation = await new NullPolicyPipelineHost()
                .PrepareExecutionAsync(ImageRequest("unregistered-prov", "unregistered-model"), CancellationToken.None);

            Assert.NotNull(preparation.Request);
            Assert.True(preparation.HasBlockingErrors);
            Assert.Contains(preparation.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "NullPolicyPipelineHost retains media on registered vision model [Windows]")]
#else
        [Fact(DisplayName = "NullPolicyPipelineHost retains media on registered vision model [Core]")]
#endif
        public async Task NullHost_KnownVision_RetainsMedia()
        {
            var registry = AIModelCapabilityRegistry.Instance;
            registry.Models["prov.m"] = new AIModelCapabilities
            {
                Provider = "prov",
                Model = "m",
                Capabilities = AICapability.Text2Text | AICapability.ImageInput,
            };

            try
            {
                var request = ImageRequest();
                var originalBody = request.Body;
                var preparation = await new NullPolicyPipelineHost()
                    .PrepareExecutionAsync(request, CancellationToken.None);

                Assert.False(preparation.HasBlockingErrors);
                // The prepared copy retains the canonical immutable body reference.
                Assert.Same(originalBody, preparation.Request.Body);
                Assert.Contains(preparation.Request.Body.Interactions, i => i is AIInteractionImage);
            }
            finally
            {
                registry.Models.TryRemove("prov.m", out _);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "RequiredInputCapabilities sees only SendToModel tool images [Windows]")]
#else
        [Fact(DisplayName = "RequiredInputCapabilities sees only SendToModel tool images [Core]")]
#endif
        public void RequiredInputCapabilities_ToolImages_SendToModelOnly()
        {
            var displayOnly = new AIInteractionToolResult
            {
                Id = "t1",
                Name = "tool",
                Result = new JObject(),
                Images = new List<ToolResultImage>
                {
                    new ToolResultImage { ImageData = "aW1n", SendToModel = false },
                },
            };
            var modelVisible = displayOnly with
            {
                Images = new List<ToolResultImage>
                {
                    new ToolResultImage { ImageData = "aW1n", SendToModel = true },
                },
            };

            Assert.Equal(
                AICapability.None,
                new List<IAIInteraction> { displayOnly }.RequiredInputCapabilities());
            Assert.Equal(
                AICapability.ImageInput,
                new List<IAIInteraction> { modelVisible }.RequiredInputCapabilities());
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SourceReference survives With* and carries page provenance [Windows]")]
#else
        [Fact(DisplayName = "SourceReference survives With* and carries page provenance [Core]")]
#endif
        public void SourceReference_RecordedOnInteraction()
        {
            var source = new SourceContentReference
            {
                DocumentId = "doc1",
                SourceName = "paper.pdf",
                PageNumber = 3,
                Kind = SourceContentKind.Page,
                Caption = "Figure 2",
                Region = new SourceContentRegion { X = 0.1, Y = 0.2, Width = 0.5, Height = 0.4 },
            };

            var text = new AIInteractionText { Agent = AIAgent.User, Content = "ctx" }
                .WithSourceReference(source);

            Assert.Equal("doc1", text.SourceReference.DocumentId);
            Assert.Equal(3, text.SourceReference.PageNumber);
            Assert.Equal(SourceContentKind.Page, text.SourceReference.Kind);
            Assert.Equal(0.5, text.SourceReference.Region.Width);

            var moved = (AIInteractionText)text.WithTurnId("turn-1");
            Assert.Equal("turn-1", moved.TurnId);
            Assert.Same(source, moved.SourceReference);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Exec fails closed when the host returns a null preparation [Windows]")]
#else
        [Fact(DisplayName = "Exec fails closed when the host returns a null preparation [Core]")]
#endif
        public async Task Exec_NullPreparation_FailsClosed()
        {
            var previous = ProviderSdkHost.PolicyPipeline;
            try
            {
                ProviderSdkHost.PolicyPipeline = new NullReturnHost();
                var result = await ImageRequest().Exec();
                Assert.False(result.Success);
            }
            finally
            {
                ProviderSdkHost.PolicyPipeline = previous;
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Exec fails closed when the host returns a null prepared request [Windows]")]
#else
        [Fact(DisplayName = "Exec fails closed when the host returns a null prepared request [Core]")]
#endif
        public async Task Exec_NullPreparedRequest_FailsClosed()
        {
            var previous = ProviderSdkHost.PolicyPipeline;
            try
            {
                ProviderSdkHost.PolicyPipeline = new NullRequestHost();
                var result = await ImageRequest().Exec();
                Assert.False(result.Success);
            }
            finally
            {
                ProviderSdkHost.PolicyPipeline = previous;
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ValidateOutboundRequest covers modality, structural and trust diagnostics [Windows]")]
#else
        [Fact(DisplayName = "ValidateOutboundRequest covers modality, structural and trust diagnostics [Core]")]
#endif
        public void ValidateOutboundRequest_CoversAllGates()
        {
            var registry = AIModelCapabilityRegistry.Instance;
            registry.Models["prov.vision"] = new AIModelCapabilities
            {
                Provider = "prov",
                Model = "vision",
                Capabilities = AICapability.Text2Text | AICapability.ImageInput,
            };

            var provider = new Moq.Mock<SmartHopper.ProviderSdk.AIProviders.IAIProvider>();
            provider.Setup(p => p.Name).Returns("prov");
            provider.Setup(p => p.IsEnabled).Returns(true);
            provider.Setup(p => p.IsConfigured).Returns(true);
            provider.Setup(p => p.SelectModel(Moq.It.IsAny<AICapability>(), Moq.It.IsAny<string>()))
                .Returns<AICapability, string>((_, m) => m ?? string.Empty);
            var providerRegistry = new Moq.Mock<IProviderRegistryHost>();
            providerRegistry.Setup(h => h.GetProvider("prov")).Returns(provider.Object);

            var previousRegistry = ProviderSdkHost.ProviderRegistry;
            try
            {
                ProviderSdkHost.ProviderRegistry = providerRegistry.Object;

                var request = ImageRequest("prov", "vision");
                Assert.Empty(AIRequestPreparation.ValidateOutboundRequest(request));

                var blocked = ImageRequest("prov", "ghost");
                var messages = AIRequestPreparation.ValidateOutboundRequest(blocked);
                Assert.Contains(messages, m => m.Severity == SHRuntimeMessageSeverity.Error);
            }
            finally
            {
                ProviderSdkHost.ProviderRegistry = previousRegistry;
                registry.Models.TryRemove("prov.vision", out _);
            }
        }

        /// <summary>Host that returns a null preparation — must fail closed.</summary>
        private sealed class NullReturnHost : IPolicyPipelineHost
        {
            public Task ApplyRequestPoliciesAsync(AIRequestCall request) => Task.CompletedTask;

            public Task ApplyResponsePoliciesAsync(AIReturn response) => Task.CompletedTask;

            public Task<AIRequestPreparation> PrepareExecutionAsync(AIRequestCall request, CancellationToken ct = default)
                => Task.FromResult<AIRequestPreparation>(null);
        }

        /// <summary>Host that returns a preparation with a null request — must fail closed.</summary>
        private sealed class NullRequestHost : IPolicyPipelineHost
        {
            public Task ApplyRequestPoliciesAsync(AIRequestCall request) => Task.CompletedTask;

            public Task ApplyResponsePoliciesAsync(AIReturn response) => Task.CompletedTask;

            public Task<AIRequestPreparation> PrepareExecutionAsync(AIRequestCall request, CancellationToken ct = default)
                => Task.FromResult(new AIRequestPreparation(null));
        }
    }
}
