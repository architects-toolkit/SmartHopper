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

namespace SmartHopper.Infrastructure.Tests
{
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AITools;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using Xunit;

    /// <summary>
    /// Tests for the AIToolManager functionality.
    /// </summary>
    [Collection("AIToolManager")]
    public class AIToolManagerTests
    {
#if NET7_WINDOWS
        /// <summary>
        /// Tests that RegisterTool correctly adds a tool to the manager on Windows.
        /// </summary>
        [Fact(DisplayName = "RegisterTool ShouldAddTool [Windows]")]
#else
        /// <summary>
        /// Tests that RegisterTool correctly adds a tool to the manager on Core.
        /// </summary>
        [Fact(DisplayName = "RegisterTool ShouldAddTool [Core]")]
#endif
        public void RegisterTool_ShouldAddTool()
        {
            AIToolManager.ResetTools();
            var tool = new AITool("TestTool", "Test Description", "Test Category", "{}", _ => Task.FromResult(new AIReturn()));
            AIToolManager.RegisterTool(tool);
            var tools = AIToolManager.GetTools();
            Assert.Contains("TestTool", tools.Keys);
            Assert.Equal("Test Description", tools["TestTool"].Description);
            Assert.Equal("Test Category", tools["TestTool"].Category);
        }

#if NET7_WINDOWS
        /// <summary>
        /// Tests that GetTools returns an empty collection when no tools are registered on Windows.
        /// </summary>
        [Fact(DisplayName = "GetTools ShouldBeEmpty WhenNoToolsRegistered [Windows]")]
#else
        /// <summary>
        /// Tests that GetTools returns an empty collection when no tools are registered on Core.
        /// </summary>
        [Fact(DisplayName = "GetTools ShouldBeEmpty WhenNoToolsRegistered [Core]")]
#endif
        public void GetTools_ShouldBeEmpty_WhenNoToolsRegistered()
        {
            AIToolManager.ResetTools();
            var tools = AIToolManager.GetTools();
            Assert.Empty(tools);
        }

#if NET7_WINDOWS
        /// <summary>
        /// Tests that AITool.Enabled defaults to true and can be set to false.
        /// </summary>
        [Fact(DisplayName = "AITool Enabled DefaultsToTrue AndCanBeDisabled [Windows]")]
#else
        /// <summary>
        /// Tests that AITool.Enabled defaults to true and can be set to false.
        /// </summary>
        [Fact(DisplayName = "AITool Enabled DefaultsToTrue AndCanBeDisabled [Core]")]
#endif
        public void AITool_Enabled_DefaultsToTrue_AndCanBeDisabled()
        {
            var enabledTool = new AITool("EnabledTool", "Description", "Category", "{}", _ => Task.FromResult(new AIReturn()));
            var disabledTool = new AITool("DisabledTool", "Description", "Category", "{}", _ => Task.FromResult(new AIReturn()), enabled: false);

            Assert.True(enabledTool.Enabled);
            Assert.False(disabledTool.Enabled);
        }
    }
}
