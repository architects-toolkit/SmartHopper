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
    using System;
    using SmartHopper.ProviderSdk.AIModels;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AICapability"/> and its extension methods.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AICapabilityTests
    {
#if NET7_WINDOWS
        private const string PlatformSuffix = " [Windows]";
#else
        private const string PlatformSuffix = " [Core]";
#endif

        [Theory(DisplayName = nameof(ToDetailedString_FormatsCapabilities) + PlatformSuffix)]
        [InlineData(AICapability.None, "None")]
        [InlineData(AICapability.Text2Text, "TextInput, TextOutput")]
        [InlineData(AICapability.ToolChat, "TextInput, TextOutput, FunctionCalling")]
        [InlineData(AICapability.ReasoningChat, "TextInput, TextOutput, Reasoning")]
        [InlineData(AICapability.Text2Image, "TextInput, ImageOutput")]
        [InlineData(AICapability.AudioInput, "SpeechInput, AudioInput")]
        [InlineData(AICapability.AudioOutput, "SpeechOutput, AudioOutput")]
        public void ToDetailedString_FormatsCapabilities(AICapability capability, string expected)
        {
            Assert.Equal(expected, capability.ToDetailedString());
        }

        [Theory(DisplayName = nameof(HasInput_DetectsInputCapabilities) + PlatformSuffix)]
        [InlineData(AICapability.None, false)]
        [InlineData(AICapability.Text2Text, true)]
        [InlineData(AICapability.Text2Image, true)]
        [InlineData(AICapability.AudioInput, true)]
        [InlineData(AICapability.ImageOutput, false)]
        public void HasInput_DetectsInputCapabilities(AICapability capability, bool expected)
        {
            Assert.Equal(expected, capability.HasInput());
        }

        [Theory(DisplayName = nameof(HasOutput_DetectsOutputCapabilities) + PlatformSuffix)]
        [InlineData(AICapability.None, false)]
        [InlineData(AICapability.Text2Text, true)]
        [InlineData(AICapability.Text2Image, true)]
        [InlineData(AICapability.AudioOutput, true)]
        [InlineData(AICapability.TextInput, false)]
        public void HasOutput_DetectsOutputCapabilities(AICapability capability, bool expected)
        {
            Assert.Equal(expected, capability.HasOutput());
        }

        [Theory(DisplayName = nameof(HasFlag_DetectsIndividualFlags) + PlatformSuffix)]
        [InlineData(AICapability.Text2Text, AICapability.TextInput, true)]
        [InlineData(AICapability.Text2Text, AICapability.TextOutput, true)]
        [InlineData(AICapability.Text2Text, AICapability.FunctionCalling, false)]
        [InlineData(AICapability.ToolChat, AICapability.FunctionCalling, true)]
        [InlineData(AICapability.AudioInput, AICapability.SpeechInput, true)]
        [InlineData(AICapability.AudioOutput, AICapability.SpeechOutput, true)]
        public void HasFlag_DetectsIndividualFlags(AICapability capability, AICapability flag, bool expected)
        {
            Assert.Equal(expected, capability.HasFlag(flag));
        }

        [Fact(DisplayName = nameof(Text2Text_MatchesTextInputAndTextOutput) + PlatformSuffix)]
        public void Text2Text_MatchesTextInputAndTextOutput()
        {
            Assert.Equal(AICapability.TextInput | AICapability.TextOutput, AICapability.Text2Text);
        }

        [Fact(DisplayName = nameof(ToolChat_MatchesText2TextAndFunctionCalling) + PlatformSuffix)]
        public void ToolChat_MatchesText2TextAndFunctionCalling()
        {
            Assert.Equal(AICapability.Text2Text | AICapability.FunctionCalling, AICapability.ToolChat);
        }
    }
}
