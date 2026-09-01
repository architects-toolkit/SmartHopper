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

namespace SmartHopper.ProviderSdk.Tests.AICall.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.AICall.Validation;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Diagnostics;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Settings;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="ProviderTrustPolicy"/> and its integration with <see cref="AIRequestCall"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class ProviderTrustPolicyTests
    {
        private const string TestProviderName = "TestProvider";

        #region ProviderTrustPolicy

        [Fact]
        public void Evaluate_Allow_WhenNoTrustIssues()
        {
            var host = new FakeProviderTrustHost();

            var result = ProviderTrustPolicy.Evaluate(TestProviderName, host);

            Assert.Equal(ProviderTrustVerdict.Allow, result.Verdict);
            Assert.Empty(result.Messages);
        }

        [Theory]
        [InlineData(ProviderIntegrityCheckMode.Soft)]
        [InlineData(ProviderIntegrityCheckMode.Hard)]
        [InlineData(ProviderIntegrityCheckMode.Strict)]
        public void Evaluate_MismatchedProducesExpectedVerdict(ProviderIntegrityCheckMode mode)
        {
            var host = new FakeProviderTrustHost
            {
                EffectiveMode = mode,
                Mismatched = true,
            };

            var result = ProviderTrustPolicy.Evaluate(TestProviderName, host);

            if (mode == ProviderIntegrityCheckMode.Soft)
            {
                Assert.Equal(ProviderTrustVerdict.Warn, result.Verdict);
                Assert.All(result.Messages, m => Assert.Equal(SHRuntimeMessageSeverity.Warning, m.Severity));
            }
            else
            {
                Assert.Equal(ProviderTrustVerdict.Block, result.Verdict);
                Assert.All(result.Messages, m => Assert.Equal(SHRuntimeMessageSeverity.Error, m.Severity));
            }
        }

        [Theory]
        [InlineData(ProviderIntegrityCheckMode.Soft, ProviderTrustVerdict.Warn)]
        [InlineData(ProviderIntegrityCheckMode.Hard, ProviderTrustVerdict.Warn)]
        [InlineData(ProviderIntegrityCheckMode.Strict, ProviderTrustVerdict.Block)]
        public void Evaluate_UnavailableProducesExpectedVerdict(ProviderIntegrityCheckMode mode, ProviderTrustVerdict expected)
        {
            var host = new FakeProviderTrustHost
            {
                EffectiveMode = mode,
                Unavailable = true,
            };

            var result = ProviderTrustPolicy.Evaluate(TestProviderName, host);

            Assert.Equal(expected, result.Verdict);
        }

        [Theory]
        [InlineData(ProviderIntegrityCheckMode.Soft, ProviderTrustVerdict.Warn)]
        [InlineData(ProviderIntegrityCheckMode.Hard, ProviderTrustVerdict.Block)]
        [InlineData(ProviderIntegrityCheckMode.Strict, ProviderTrustVerdict.Block)]
        public void Evaluate_UnknownProducesExpectedVerdict(ProviderIntegrityCheckMode mode, ProviderTrustVerdict expected)
        {
            var host = new FakeProviderTrustHost
            {
                EffectiveMode = mode,
                Unknown = true,
            };

            var result = ProviderTrustPolicy.Evaluate(TestProviderName, host);

            Assert.Equal(expected, result.Verdict);
        }

        [Fact]
        public void Evaluate_Hard_BlocksMismatchedAndUnknown_WarnsUnavailable()
        {
            var host = new FakeProviderTrustHost
            {
                EffectiveMode = ProviderIntegrityCheckMode.Hard,
                Mismatched = true,
                Unavailable = true,
                Unknown = true,
            };

            var result = ProviderTrustPolicy.Evaluate(TestProviderName, host);

            Assert.Equal(ProviderTrustVerdict.Block, result.Verdict);
            Assert.Equal(2, result.Messages.Count(m => m.Severity == SHRuntimeMessageSeverity.Error));
            Assert.Single(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Warning);
        }

        [Fact]
        public void Evaluate_Hard_BlocksUnknown_WarnsCommunity()
        {
            var host = new FakeProviderTrustHost
            {
                EffectiveMode = ProviderIntegrityCheckMode.Hard,
                Unknown = true,
                Community = true,
            };

            var result = ProviderTrustPolicy.Evaluate(TestProviderName, host);

            Assert.Equal(ProviderTrustVerdict.Block, result.Verdict);
            Assert.Single(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error);
            Assert.Single(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Warning);
        }

        [Fact]
        public void Evaluate_Strict_BlocksMismatchedUnavailableUnknown_WarnsCommunity()
        {
            var host = new FakeProviderTrustHost
            {
                EffectiveMode = ProviderIntegrityCheckMode.Strict,
                Mismatched = true,
                Unavailable = true,
                Unknown = true,
                Community = true,
            };

            var result = ProviderTrustPolicy.Evaluate(TestProviderName, host);

            Assert.Equal(ProviderTrustVerdict.Block, result.Verdict);
            Assert.Equal(3, result.Messages.Count(m => m.Severity == SHRuntimeMessageSeverity.Error));
            Assert.Single(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Warning);
        }

        [Fact]
        public void Evaluate_EmptyProviderName_Allows()
        {
            var host = new FakeProviderTrustHost { Unknown = true };

            var result = ProviderTrustPolicy.Evaluate(string.Empty, host);

            Assert.Equal(ProviderTrustVerdict.Allow, result.Verdict);
            Assert.Empty(result.Messages);
        }

        #endregion

        #region AIRequestCall integration

        [Fact]
        public void IsValid_Blocks_WhenTrustPolicyReturnsBlock()
        {
            var originalTrust = ProviderSdkHost.ProviderTrust;

            try
            {
                ProviderSdkHost.ProviderTrust = new FakeProviderTrustHost
                {
                    EffectiveMode = ProviderIntegrityCheckMode.Hard,
                    Unknown = true,
                };

                var request = new AIRequestCall
                {
                    Provider = TestProviderName,
                    Endpoint = "https://test",
                    RequestKind = AIRequestKind.Backoffice,
                };

                var (isValid, messages) = request.IsValid();

                Assert.False(isValid);
                Assert.Contains(messages, m => m.Severity == SHRuntimeMessageSeverity.Error);
                Assert.Contains(messages, m => m.Message.Contains("not known", StringComparison.Ordinal));
            }
            finally
            {
                ProviderSdkHost.ProviderTrust = originalTrust;
            }
        }

        [Fact]
        public void IsValid_Warns_WhenTrustPolicyReturnsWarn()
        {
            var originalTrust = ProviderSdkHost.ProviderTrust;
            var originalRegistry = ProviderSdkHost.ProviderRegistry;

            try
            {
                ProviderSdkHost.ProviderTrust = new FakeProviderTrustHost
                {
                    EffectiveMode = ProviderIntegrityCheckMode.Soft,
                    Unknown = true,
                };

                var request = new AIRequestCall
                {
                    Provider = TestProviderName,
                    Endpoint = "https://test",
                    RequestKind = AIRequestKind.Backoffice,
                };

                var (isValid, messages) = request.IsValid();

                Assert.Contains(messages, m => m.Severity == SHRuntimeMessageSeverity.Warning);
                Assert.Contains(messages, m => m.Message.Contains("not known", StringComparison.Ordinal));
            }
            finally
            {
                ProviderSdkHost.ProviderTrust = originalTrust;
                ProviderSdkHost.ProviderRegistry = originalRegistry;
            }
        }

        [Fact]
        public async Task Exec_Blocks_WhenTrustPolicyReturnsBlock()
        {
            var originalTrust = ProviderSdkHost.ProviderTrust;
            var originalRegistry = ProviderSdkHost.ProviderRegistry;

            try
            {
                ProviderSdkHost.ProviderTrust = new FakeProviderTrustHost
                {
                    EffectiveMode = ProviderIntegrityCheckMode.Hard,
                    Unknown = true,
                };

                ProviderSdkHost.ProviderRegistry = new NullProviderRegistryHost();

                var request = new AIRequestCall
                {
                    Provider = TestProviderName,
                    Endpoint = "https://test",
                    RequestKind = AIRequestKind.Backoffice,
                };

                var result = await request.Exec().ConfigureAwait(false);

                Assert.NotNull(result);
                Assert.Equal(AICallStatus.Finished, result.Status);
                Assert.Contains(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Error);
                Assert.Contains(result.Messages, m => m.Message.Contains("not known", StringComparison.Ordinal));
            }
            finally
            {
                ProviderSdkHost.ProviderTrust = originalTrust;
                ProviderSdkHost.ProviderRegistry = originalRegistry;
            }
        }

        [Fact]
        public async Task Exec_WarnsAndDoesNotBlock_WhenTrustPolicyReturnsWarn()
        {
            var originalTrust = ProviderSdkHost.ProviderTrust;
            var originalRegistry = ProviderSdkHost.ProviderRegistry;

            try
            {
                ProviderSdkHost.ProviderTrust = new FakeProviderTrustHost
                {
                    EffectiveMode = ProviderIntegrityCheckMode.Soft,
                    Unknown = true,
                };

                ProviderSdkHost.ProviderRegistry = new NullProviderRegistryHost();

                var request = new AIRequestCall
                {
                    Provider = TestProviderName,
                    Endpoint = "https://test",
                    RequestKind = AIRequestKind.Backoffice,
                };

                var result = await request.Exec().ConfigureAwait(false);

                Assert.NotNull(result);
                Assert.Contains(result.Messages, m => m.Severity == SHRuntimeMessageSeverity.Warning);
                Assert.Contains(result.Messages, m => m.Message.Contains("not known", StringComparison.Ordinal));
            }
            finally
            {
                ProviderSdkHost.ProviderTrust = originalTrust;
                ProviderSdkHost.ProviderRegistry = originalRegistry;
            }
        }

        #endregion

        private sealed class FakeProviderTrustHost : IProviderTrustHost
        {
            public ProviderIntegrityCheckMode EffectiveMode { get; set; } = ProviderIntegrityCheckMode.Soft;

            public bool Mismatched { get; set; }

            public bool Unavailable { get; set; }

            public bool Unknown { get; set; }

            public bool Community { get; set; }

            public bool Unsigned { get; set; }

            public ProviderIntegrityCheckMode EffectiveIntegrityCheckMode => this.EffectiveMode;

            public bool IsProviderMismatched(string providerName) => this.Mismatched;

            public bool IsProviderUnavailable(string providerName) => this.Unavailable;

            public bool IsProviderUnknown(string providerName) => this.Unknown;

            public bool IsProviderCommunity(string providerName) => this.Community;

            public bool IsProviderUnsigned(string providerName) => this.Unsigned;
        }

        private sealed class NullProviderRegistryHost : IProviderRegistryHost
        {
            public IAIProvider GetProvider(string providerName) => null;

            public IAIProviderSettings GetProviderSettings(string providerName) => null;
        }
    }
}
