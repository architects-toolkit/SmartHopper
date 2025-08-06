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
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AITools;
    using SmartHopper.Infrastructure.AIProviders;
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
            var tool = new AITool("TestTool", "Test Description", "Test Category", "{}", _ => Task.FromResult((object)"dummy"));
            AIToolManager.RegisterTool(tool);
            var tools = AIToolManager.GetTools();
            Assert.Contains("TestTool", tools.Keys);
            Assert.Equal("Test Description", tools["TestTool"].Description);
            Assert.Equal("Test Category", tools["TestTool"].Category);
        }

#if NET7_WINDOWS
        /// <summary>
        /// Tests that ExecuteTool returns an error when the tool is not found on Windows.
        /// </summary>
        [Fact(DisplayName = "ExecuteTool ShouldReturnError WhenToolNotFound [Windows]")]
#else
        /// <summary>
        /// Tests that ExecuteTool returns an error when the tool is not found on Core.
        /// </summary>
        [Fact(DisplayName = "ExecuteTool ShouldReturnError WhenToolNotFound [Core]")]
#endif
        public async Task ExecuteTool_ShouldReturnError_WhenToolNotFound()
        {
            ResetManager();
            var result = await AIToolManager.ExecuteTool("UnknownTool", [], null!).ConfigureAwait(false);
            dynamic dyn = result;
            Assert.False(dyn.success);
            Assert.Contains("UnknownTool", (string)dyn.error, StringComparison.Ordinal);
        }

#if NET7_WINDOWS
        /// <summary>
        /// Tests that ExecuteTool correctly executes a registered tool with merged parameters on Windows.
        /// </summary>
        [Fact(DisplayName = "ExecuteTool ShouldExecuteRegisteredTool WithMergedParameters [Windows]")]
#else
        /// <summary>
        /// Tests that ExecuteTool correctly executes a registered tool with merged parameters on Core.
        /// </summary>
        [Fact(DisplayName = "ExecuteTool ShouldExecuteRegisteredTool WithMergedParameters [Core]")]
#endif
        public async Task ExecuteTool_ShouldExecuteRegisteredTool_WithMergedParameters()
        {
            ResetManager();
            JObject? captured = null;
            var tool = new AITool("Compute", "Computes value", "Test Category", "{}", p =>
            {
                captured = p;
                int value = p["value"]?.Value<int>() ?? 0;
                return Task.FromResult((object)(value * 2));
            });
            AIToolManager.RegisterTool(tool);
            var parameters = new JObject { ["value"] = 10 };
            var extra = new JObject { ["extra"] = 5 };
            var result = await AIToolManager.ExecuteTool("Compute", (JObject)parameters.DeepClone(), extra).ConfigureAwait(false);
            Assert.IsType<int>(result);
            Assert.Equal(20, (int)result);
            Assert.NotNull(captured);
            Assert.Equal(5, captured["extra"]?.Value<int>() ?? 0);
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
