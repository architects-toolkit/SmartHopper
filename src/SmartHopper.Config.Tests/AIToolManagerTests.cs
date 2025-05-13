using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;

namespace SmartHopper.Config.Tests
{
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
