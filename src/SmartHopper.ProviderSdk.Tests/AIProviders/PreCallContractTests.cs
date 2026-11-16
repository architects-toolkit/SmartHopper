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

namespace SmartHopper.ProviderSdk.Tests.AIProviders
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Contract tests for the <see cref="AIProvider.PreCallAsync"/> immutability boundary:
    /// providers resolve wire transport fields on a detached execution copy, the caller's
    /// canonical request is never mutated, and <see cref="AIRequestCall.Source"/> keeps
    /// the canonical lineage across copy generations.
    /// </summary>
    [Collection("ProviderSdk")]
    public class PreCallContractTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "PreCallAsync returns detached copy, input untouched [Windows]")]
#else
        [Fact(DisplayName = "PreCallAsync returns detached copy, input untouched [Core]")]
#endif
        public async Task PreCallAsync_ReturnsDetachedCopy_InputUntouched()
        {
            var provider = new MutatingProvider();
            var input = BuildRequest();

            var outbound = await provider.PreCallAsync(input, CancellationToken.None).ConfigureAwait(false);

            Assert.NotSame(input, outbound);
            Assert.Equal("/wire/path", outbound.Endpoint);
            Assert.Equal("1", outbound.Headers["X-Wire"]);

            // The caller's canonical request keeps semantic state and no wire residue.
            Assert.Null(input.Endpoint);
            Assert.DoesNotContain("X-Wire", input.Headers.Keys);
            Assert.Equal("gh_put", input.Origin);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "PreCallAsync preserves Origin on the prepared copy [Windows]")]
#else
        [Fact(DisplayName = "PreCallAsync preserves Origin on the prepared copy [Core]")]
#endif
        public async Task PreCallAsync_PreservesOrigin()
        {
            var provider = new MutatingProvider();
            var input = BuildRequest();

            var outbound = await provider.PreCallAsync(input, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal("gh_put", outbound.Origin);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "PreCallAsync stamps Source to the canonical request [Windows]")]
#else
        [Fact(DisplayName = "PreCallAsync stamps Source to the canonical request [Core]")]
#endif
        public async Task PreCallAsync_StampsSource()
        {
            var provider = new MutatingProvider();
            var input = BuildRequest();

            var outbound = await provider.PreCallAsync(input, CancellationToken.None).ConfigureAwait(false);

            Assert.Same(input, outbound.Source);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Execution copies flatten Source lineage to the root request [Windows]")]
#else
        [Fact(DisplayName = "Execution copies flatten Source lineage to the root request [Core]")]
#endif
        public void CreateExecutionCopy_FlattensSourceLineage()
        {
            var canonical = BuildRequest();

            var copy1 = canonical.CreateExecutionCopy();
            var copy2 = copy1.CreateExecutionCopy();

            Assert.Same(canonical, copy1.Source);
            Assert.Same(canonical, copy2.Source);
            Assert.Null(canonical.Source);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "PreCallAsync preserves a caller-set wire endpoint on the copy [Windows]")]
#else
        [Fact(DisplayName = "PreCallAsync preserves a caller-set wire endpoint on the copy [Core]")]
#endif
        public async Task PreCallAsync_PreservesCallerWireEndpoint()
        {
            var provider = new MutatingProvider { MutateEndpoint = false };
            var input = BuildRequest();
            input.Endpoint = "/models";

            var outbound = await provider.PreCallAsync(input, CancellationToken.None).ConfigureAwait(false);

            // The wire-path override survives on the copy; the input keeps it too.
            Assert.Equal("/models", outbound.Endpoint);
            Assert.Equal("/models", input.Endpoint);
        }

        private static AIRequestCall BuildRequest()
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: MutatingProvider.ProviderName,
                model: "fake-model",
                body: AIBodyBuilder.Create().AddUser("hello").Build(),
                origin: "gh_put",
                capability: AICapability.Text2Text);
            return request;
        }

        /// <summary>
        /// A provider that mutates the outbound copy the way real providers do
        /// (endpoint, method, headers) to prove the input never receives the residue.
        /// </summary>
        private sealed class MutatingProvider : AIProvider
        {
            public const string ProviderName = "MutatingProvider";

            public MutatingProvider()
            {
                this.Models = new FakeProviderModels();
            }

            public bool MutateEndpoint { get; set; } = true;

            public override string Name => ProviderName;

            public override Image Icon => new Bitmap(1, 1);

            public override bool IsEnabled => true;

            public override bool IsConfigured => true;

            public override System.Uri DefaultServerUrl => new System.Uri("https://fake.example/");

            public override string Encode(AIRequestCall request) => "{}";

            public override string Encode(IAIInteraction interaction) => "{}";

            public override List<IAIInteraction> Decode(JObject response) => new List<IAIInteraction>();

            protected override Task<AIRequestCall> PreCallCoreAsync(AIRequestCall outbound, CancellationToken cancellationToken)
            {
                var result = base.PreCallCoreAsync(outbound, cancellationToken);
                if (this.MutateEndpoint)
                {
                    outbound.Endpoint = "/wire/path";
                }

                outbound.HttpMethod = "POST";
                outbound.Headers["X-Wire"] = "1";
                return result;
            }
        }
    }
}
