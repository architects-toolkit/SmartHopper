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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using Xunit;

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Interactions
{
    /// <summary>
    /// Tests for <see cref="OpenAICompatibleRoleMapper"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class OpenAICompatibleRoleMapperTests
    {
        /// <summary>
        /// Core chat agents must map to their expected OpenAI-compatible roles.
        /// </summary>
        [Theory]
        [InlineData(AIAgent.System, "system")]
        [InlineData(AIAgent.Context, "system")]
        [InlineData(AIAgent.User, "user")]
        [InlineData(AIAgent.Assistant, "assistant")]
        [InlineData(AIAgent.ToolCall, "assistant")]
        [InlineData(AIAgent.ToolResult, "tool")]
        public void MapRole_KnownAgent_ReturnsExpectedRole(AIAgent agent, string expected)
        {
            var role = OpenAICompatibleRoleMapper.MapRole(agent);

            Assert.Equal(expected, role);
        }

        /// <summary>
        /// UI-only or unknown agents must return <c>null</c> so callers can skip them.
        /// </summary>
        [Theory]
        [InlineData(AIAgent.Summary)]
        [InlineData(AIAgent.Error)]
        [InlineData(AIAgent.Warning)]
        [InlineData(AIAgent.Info)]
        [InlineData(AIAgent.Debug)]
        [InlineData(AIAgent.Unknown)]
        public void MapRole_UnsupportedAgent_ReturnsNull(AIAgent agent)
        {
            var role = OpenAICompatibleRoleMapper.MapRole(agent);

            Assert.Null(role);
        }

        /// <summary>
        /// <see cref="OpenAICompatibleRoleMapper.MapRoleOrThrow"/> returns the mapped role
        /// for supported agents.
        /// </summary>
        [Fact(DisplayName = "MapRoleOrThrow: supported agent returns role")]
        public void MapRoleOrThrow_SupportedAgent_ReturnsRole()
        {
            var role = OpenAICompatibleRoleMapper.MapRoleOrThrow(AIAgent.User, "TestProvider");

            Assert.Equal("user", role);
        }

        /// <summary>
        /// <see cref="OpenAICompatibleRoleMapper.MapRoleOrThrow"/> throws for unsupported agents
        /// and includes the provider name in the message.
        /// </summary>
        [Fact(DisplayName = "MapRoleOrThrow: unsupported agent throws ArgumentException")]
        public void MapRoleOrThrow_UnsupportedAgent_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => OpenAICompatibleRoleMapper.MapRoleOrThrow(AIAgent.Warning, "TestProvider"));

            Assert.Contains("TestProvider", ex.Message);
        }
    }
}
