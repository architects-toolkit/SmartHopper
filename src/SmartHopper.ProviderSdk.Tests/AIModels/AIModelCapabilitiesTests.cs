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

namespace SmartHopper.ProviderSdk.Tests.AIModels
{
    using System.Collections.Generic;
    using SmartHopper.ProviderSdk.AIModels;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIModelCapabilities"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIModelCapabilitiesTests
    {
#if NET7_WINDOWS
        private const string PlatformSuffix = " [Windows]";
#else
        private const string PlatformSuffix = " [Core]";
#endif

        [Fact(DisplayName = nameof(HasCapability_ReturnsTrueForNone) + PlatformSuffix)]
        public void HasCapability_ReturnsTrueForNone()
        {
            var capabilities = new AIModelCapabilities { Capabilities = AICapability.Text2Text };

            Assert.True(capabilities.HasCapability(AICapability.None));
        }

        [Fact(DisplayName = nameof(HasCapability_ReturnsTrueForMatchingCapability) + PlatformSuffix)]
        public void HasCapability_ReturnsTrueForMatchingCapability()
        {
            var capabilities = new AIModelCapabilities { Capabilities = AICapability.Text2Text };

            Assert.True(capabilities.HasCapability(AICapability.Text2Text));
            Assert.True(capabilities.HasCapability(AICapability.TextInput));
            Assert.True(capabilities.HasCapability(AICapability.TextOutput));
        }

        [Fact(DisplayName = nameof(HasCapability_ReturnsFalseForMissingFlag) + PlatformSuffix)]
        public void HasCapability_ReturnsFalseForMissingFlag()
        {
            var capabilities = new AIModelCapabilities { Capabilities = AICapability.Text2Text };

            Assert.False(capabilities.HasCapability(AICapability.ImageOutput));
            Assert.False(capabilities.HasCapability(AICapability.FunctionCalling));
        }

        [Fact(DisplayName = nameof(GetKey_LowercasesProviderAndModel) + PlatformSuffix)]
        public void GetKey_LowercasesProviderAndModel()
        {
            var capabilities = new AIModelCapabilities
            {
                Provider = "OpenAI",
                Model = "GPT-4",
            };

            Assert.Equal("openai.gpt-4", capabilities.GetKey());
        }

        [Fact(DisplayName = nameof(IsDiscouragedForAnyTool_EmptyListReturnsFalse) + PlatformSuffix)]
        public void IsDiscouragedForAnyTool_EmptyListReturnsFalse()
        {
            var capabilities = new AIModelCapabilities
            {
                DiscouragedForTools = new List<string>(),
            };

            Assert.False(capabilities.IsDiscouragedForAnyTool(new List<string> { "Tool" }));
        }

        [Fact(DisplayName = nameof(IsDiscouragedForAnyTool_WildcardMatchesAll) + PlatformSuffix)]
        public void IsDiscouragedForAnyTool_WildcardMatchesAll()
        {
            var capabilities = new AIModelCapabilities
            {
                DiscouragedForTools = new List<string> { "*" },
            };

            Assert.True(capabilities.IsDiscouragedForAnyTool(new List<string> { "AnyTool" }));
        }

        [Fact(DisplayName = nameof(IsDiscouragedForAnyTool_MatchesToolName) + PlatformSuffix)]
        public void IsDiscouragedForAnyTool_MatchesToolName()
        {
            var capabilities = new AIModelCapabilities
            {
                DiscouragedForTools = new List<string> { "MyTool" },
            };

            Assert.True(capabilities.IsDiscouragedForAnyTool(new List<string> { "MyTool" }));
        }

        [Fact(DisplayName = nameof(IsDiscouragedForAnyTool_NoMatchReturnsFalse) + PlatformSuffix)]
        public void IsDiscouragedForAnyTool_NoMatchReturnsFalse()
        {
            var capabilities = new AIModelCapabilities
            {
                DiscouragedForTools = new List<string> { "OtherTool" },
            };

            Assert.False(capabilities.IsDiscouragedForAnyTool(new List<string> { "MyTool" }));
        }

        [Fact(DisplayName = nameof(IsDiscouragedForAnyTool_MatchesCaseInsensitive) + PlatformSuffix)]
        public void IsDiscouragedForAnyTool_MatchesCaseInsensitive()
        {
            var capabilities = new AIModelCapabilities
            {
                DiscouragedForTools = new List<string> { "Tool-A" },
            };

            Assert.True(capabilities.IsDiscouragedForAnyTool(new List<string> { "tool-a" }));
        }
    }
}
