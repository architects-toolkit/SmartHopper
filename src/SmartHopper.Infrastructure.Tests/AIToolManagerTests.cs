/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.Tests
{
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AITools;
    using Xunit;

    /// <summary>
    /// Tests for the AIToolManager functionality.
    /// </summary>
    public class AIToolManagerTests
    {
        /// <summary>
        /// Resets the AIToolManager to a clean state for testing.
        /// </summary>
        private static void ResetManager()
        {
            var managerType = typeof(AIToolManager);
            var toolsField = managerType.GetField("_tools", BindingFlags.NonPublic | BindingFlags.Static);
            var discoveredField = managerType.GetField("_toolsDiscovered", BindingFlags.NonPublic | BindingFlags.Static);
            var toolsDict = (Dictionary<string, AITool>?)toolsField?.GetValue(null);
            toolsDict?.Clear();
            discoveredField?.SetValue(null, false);
        }

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
            ResetManager();
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
            ResetManager();
            var tools = AIToolManager.GetTools();
            Assert.Empty(tools);
        }
    }
}
