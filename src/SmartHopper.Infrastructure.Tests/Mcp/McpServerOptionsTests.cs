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

namespace SmartHopper.Infrastructure.Tests.Mcp
{
    using SmartHopper.Infrastructure.Mcp;
    using Xunit;

    public class McpServerOptionsTests
    {
        [Fact]
        public void DefaultPort_MatchesDocumentedConstant()
        {
            Assert.Equal(26929, McpServerOptions.DefaultPort);
            Assert.Equal(McpServerOptions.DefaultPort, new McpServerOptions().Port);
        }

        [Fact]
        public void Clone_ProducesIndependentCopy()
        {
            var original = new McpServerOptions
            {
                Port = 4242,
                BearerToken = "abc",
                EnabledTools = new[] { "gh_get" },
                ExposeMutatingTools = true,
                ServerName = "custom",
                ServerVersion = "1.2.3",
            };

            var copy = original.Clone();

            Assert.Equal(original.Port, copy.Port);
            Assert.Equal(original.BearerToken, copy.BearerToken);
            Assert.Equal(original.EnabledTools, copy.EnabledTools);
            Assert.NotSame(original.EnabledTools, copy.EnabledTools);
            Assert.Equal(original.ExposeMutatingTools, copy.ExposeMutatingTools);
            Assert.Equal(original.ServerName, copy.ServerName);
            Assert.Equal(original.ServerVersion, copy.ServerVersion);
        }
    }
}
