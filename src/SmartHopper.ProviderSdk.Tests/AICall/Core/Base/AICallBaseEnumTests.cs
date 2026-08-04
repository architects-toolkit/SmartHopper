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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Base
{
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AICallStatusExtensions"/>, <see cref="AIAgentExtensions"/> and <see cref="AIRequestKind"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AICallBaseEnumTests
    {
#if NET7_WINDOWS
        [Theory(DisplayName = "AICallStatusExtensions_ToString_ReturnsExpected [Windows]")]
#else
        [Theory(DisplayName = "AICallStatusExtensions_ToString_ReturnsExpected [Core]")]
#endif
        [InlineData(AICallStatus.Idle, "idle")]
        [InlineData(AICallStatus.Processing, "processing")]
        [InlineData(AICallStatus.Streaming, "streaming")]
        [InlineData(AICallStatus.CallingTools, "calling_tools")]
        [InlineData(AICallStatus.Finished, "finished")]
        public void AICallStatusExtensions_ToString_ReturnsExpected(AICallStatus status, string expected)
        {
            Assert.Equal(expected, AICallStatusExtensions.ToString(status));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "AICallStatusExtensions_ToDescription_ReturnsExpected [Windows]")]
#else
        [Theory(DisplayName = "AICallStatusExtensions_ToDescription_ReturnsExpected [Core]")]
#endif
        [InlineData(AICallStatus.Idle, "Idle")]
        [InlineData(AICallStatus.Processing, "Processing")]
        [InlineData(AICallStatus.Streaming, "Streaming")]
        [InlineData(AICallStatus.CallingTools, "Calling tools")]
        [InlineData(AICallStatus.Finished, "Finished")]
        public void AICallStatusExtensions_ToDescription_ReturnsExpected(AICallStatus status, string expected)
        {
            Assert.Equal(expected, AICallStatusExtensions.ToDescription(status));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AICallStatusExtensions_ToString_UnknownFallback [Windows]")]
#else
        [Fact(DisplayName = "AICallStatusExtensions_ToString_UnknownFallback [Core]")]
#endif
        public void AICallStatusExtensions_ToString_UnknownFallback()
        {
            var unknown = (AICallStatus)999;
            Assert.Equal("unknown", AICallStatusExtensions.ToString(unknown));
            Assert.Equal("Unknown", AICallStatusExtensions.ToDescription(unknown));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "AICallStatusExtensions_FromString_ReturnsExpected [Windows]")]
#else
        [Theory(DisplayName = "AICallStatusExtensions_FromString_ReturnsExpected [Core]")]
#endif
        [InlineData("idle", AICallStatus.Idle)]
        [InlineData("processing", AICallStatus.Processing)]
        [InlineData("streaming", AICallStatus.Streaming)]
        [InlineData("calling_tools", AICallStatus.CallingTools)]
        [InlineData("finished", AICallStatus.Finished)]
        public void AICallStatusExtensions_FromString_ReturnsExpected(string input, AICallStatus expected)
        {
            Assert.Equal(expected, AICallStatusExtensions.FromString(input));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AICallStatusExtensions_FromString_UnknownFallback [Windows]")]
#else
        [Fact(DisplayName = "AICallStatusExtensions_FromString_UnknownFallback [Core]")]
#endif
        public void AICallStatusExtensions_FromString_UnknownFallback()
        {
            Assert.Equal(AICallStatus.Idle, AICallStatusExtensions.FromString("not_a_status"));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "AIAgentExtensions_ToString_ReturnsExpected [Windows]")]
#else
        [Theory(DisplayName = "AIAgentExtensions_ToString_ReturnsExpected [Core]")]
#endif
        [InlineData(AIAgent.Context, "context")]
        [InlineData(AIAgent.System, "system")]
        [InlineData(AIAgent.User, "user")]
        [InlineData(AIAgent.Assistant, "assistant")]
        [InlineData(AIAgent.ToolCall, "tool_call")]
        [InlineData(AIAgent.ToolResult, "tool_result")]
        [InlineData(AIAgent.Summary, "summary")]
        [InlineData(AIAgent.Error, "error")]
        [InlineData(AIAgent.Warning, "warning")]
        [InlineData(AIAgent.Info, "info")]
        [InlineData(AIAgent.Debug, "debug")]
        [InlineData(AIAgent.Unknown, "unknown")]
        public void AIAgentExtensions_ToString_ReturnsExpected(AIAgent agent, string expected)
        {
            Assert.Equal(expected, AIAgentExtensions.ToString(agent));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "AIAgentExtensions_ToDescription_ReturnsExpected [Windows]")]
#else
        [Theory(DisplayName = "AIAgentExtensions_ToDescription_ReturnsExpected [Core]")]
#endif
        [InlineData(AIAgent.Context, "Context")]
        [InlineData(AIAgent.System, "System")]
        [InlineData(AIAgent.User, "User")]
        [InlineData(AIAgent.Assistant, "Assistant")]
        [InlineData(AIAgent.ToolCall, "Tool Call")]
        [InlineData(AIAgent.ToolResult, "Tool Result")]
        [InlineData(AIAgent.Summary, "Summary")]
        [InlineData(AIAgent.Error, "Error")]
        [InlineData(AIAgent.Warning, "Warning")]
        [InlineData(AIAgent.Info, "Info")]
        [InlineData(AIAgent.Debug, "Debug")]
        [InlineData(AIAgent.Unknown, "Unknown")]
        public void AIAgentExtensions_ToDescription_ReturnsExpected(AIAgent agent, string expected)
        {
            Assert.Equal(expected, AIAgentExtensions.ToDescription(agent));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "AIAgentExtensions_FromString_ReturnsExpected [Windows]")]
#else
        [Theory(DisplayName = "AIAgentExtensions_FromString_ReturnsExpected [Core]")]
#endif
        [InlineData("context", AIAgent.Context)]
        [InlineData("system", AIAgent.System)]
        [InlineData("developer", AIAgent.System)]
        [InlineData("user", AIAgent.User)]
        [InlineData("assistant", AIAgent.Assistant)]
        [InlineData("tool_call", AIAgent.ToolCall)]
        [InlineData("tool_result", AIAgent.ToolResult)]
        [InlineData("tool", AIAgent.ToolResult)]
        [InlineData("summary", AIAgent.Summary)]
        [InlineData("error", AIAgent.Error)]
        [InlineData("warning", AIAgent.Warning)]
        [InlineData("info", AIAgent.Info)]
        [InlineData("debug", AIAgent.Debug)]
        [InlineData("unknown", AIAgent.Unknown)]
        public void AIAgentExtensions_FromString_ReturnsExpected(string input, AIAgent expected)
        {
            Assert.Equal(expected, AIAgentExtensions.FromString(input));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIAgentExtensions_FromString_UnknownFallback [Windows]")]
#else
        [Fact(DisplayName = "AIAgentExtensions_FromString_UnknownFallback [Core]")]
#endif
        public void AIAgentExtensions_FromString_UnknownFallback()
        {
            Assert.Equal(AIAgent.Unknown, AIAgentExtensions.FromString("not_an_agent"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestKind_Values [Windows]")]
#else
        [Fact(DisplayName = "AIRequestKind_Values [Core]")]
#endif
        public void AIRequestKind_Values()
        {
            Assert.Equal(0, (int)AIRequestKind.Generation);
            Assert.Equal(1, (int)AIRequestKind.Backoffice);
        }
    }
}
