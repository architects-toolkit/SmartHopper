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
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.Managers.AITools;
    using SmartHopper.Infrastructure.Models;
    using Xunit;

    public class AIToolManagerTests
    {
        private void ResetManager()
        {
            var managerType = typeof(AIToolManager);
            var toolsField = managerType.GetField("_tools", BindingFlags.NonPublic | BindingFlags.Static);
            var discoveredField = managerType.GetField("_toolsDiscovered", BindingFlags.NonPublic | BindingFlags.Static);
            var toolsDict = (Dictionary<string, AITool>)toolsField.GetValue(null);
            toolsDict.Clear();
            discoveredField.SetValue(null, false);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "RegisterTool_ShouldAddTool [Windows]")]
#else
        [Fact(DisplayName = "RegisterTool_ShouldAddTool [Core]")]
#endif
        public void RegisterTool_ShouldAddTool()
        {
            ResetManager();
            var tool = new AITool("TestTool", "Test Description", "{}", _ => Task.FromResult((object)"dummy"));
            AIToolManager.RegisterTool(tool);
            var tools = AIToolManager.GetTools();
            Assert.Contains("TestTool", tools.Keys);
            Assert.Equal("Test Description", tools["TestTool"].Description);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ExecuteTool_ShouldReturnError_WhenToolNotFound [Windows]")]
#else
        [Fact(DisplayName = "ExecuteTool_ShouldReturnError_WhenToolNotFound [Core]")]
#endif
        public async Task ExecuteTool_ShouldReturnError_WhenToolNotFound()
        {
            ResetManager();
            var result = await AIToolManager.ExecuteTool("UnknownTool", new JObject(), null);
            dynamic dyn = result;
            Assert.False(dyn.success);
            Assert.Contains("UnknownTool", (string)dyn.error);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ExecuteTool_ShouldExecuteRegisteredTool_WithMergedParameters [Windows]")]
#else
        [Fact(DisplayName = "ExecuteTool_ShouldExecuteRegisteredTool_WithMergedParameters [Core]")]
#endif
        public async Task ExecuteTool_ShouldExecuteRegisteredTool_WithMergedParameters()
        {
            ResetManager();
            JObject captured = null;
            var tool = new AITool("Compute", "Computes value", "{}", p =>
            {
                captured = p;
                int value = p["value"].Value<int>();
                return Task.FromResult((object)(value * 2));
            });
            AIToolManager.RegisterTool(tool);
            var parameters = new JObject { ["value"] = 10 };
            var extra = new JObject { ["extra"] = 5 };
            var result = await AIToolManager.ExecuteTool("Compute", (JObject)parameters.DeepClone(), extra);
            Assert.IsType<int>(result);
            Assert.Equal(20, (int)result);
            Assert.Equal(5, captured["extra"].Value<int>());
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetTools_ShouldBeEmpty_WhenNoToolsRegistered [Windows]")]
#else
        [Fact(DisplayName = "GetTools_ShouldBeEmpty_WhenNoToolsRegistered [Core]")]
#endif
        public void GetTools_ShouldBeEmpty_WhenNoToolsRegistered()
        {
            ResetManager();
            var tools = AIToolManager.GetTools();
            Assert.Empty(tools);
        }
    }
}
