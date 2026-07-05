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
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AITools;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="AIToolManager.ExecuteTool"/> method.
    /// </summary>
    [Collection("AIToolManager")]
    public class AIToolManagerExecutionTests
    {
        #region ExecuteTool Validation

#if NET7_WINDOWS
        [Fact(DisplayName = "AIToolManager ExecuteTool invalid tool call returns error [Windows]")]
#else
        [Fact(DisplayName = "AIToolManager ExecuteTool invalid tool call returns error [Core]")]
#endif
        public async Task ExecuteTool_InvalidToolCall_ReturnsError()
        {
            AIToolManager.ResetTools();

            // A tool call with a pending tool but missing provider/endpoint fails base validation.
            var toolCall = new AIToolCall
            {
                Provider = null,
                Model = null,
                Body = AIBodyBuilder.Create()
                    .WithTurnId(System.Guid.NewGuid().ToString("N"))
                    .Add(new AIInteractionToolCall
                    {
                        Id = "call-1",
                        Name = "some_tool",
                        Arguments = new JObject(),
                    })
                    .Build(),
            };

            var result = await AIToolManager.ExecuteTool(toolCall).ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.True(result.Messages.Exists(m => m.Severity == Diagnostics.SHRuntimeMessageSeverity.Error));
        }

        #endregion

        #region ExecuteTool Happy Path

#if NET7_WINDOWS
        [Fact(DisplayName = "AIToolManager ExecuteTool registered tool executes and returns result [Windows]")]
#else
        [Fact(DisplayName = "AIToolManager ExecuteTool registered tool executes and returns result [Core]")]
#endif
        public async Task ExecuteTool_RegisteredTool_ExecutesAndReturnsResult()
        {
            AIToolManager.ResetTools();

            var tool = new AITool("test_echo", "Echo tool", "test", "{}", async request =>
            {
                var ret = new AIReturn();
                var body = AIBodyBuilder.Create()
                    .WithTurnId(System.Guid.NewGuid().ToString("N"))
                    .AddText(AIAgent.ToolResult, "echo-result")
                    .Build();
                ret.SetBody(body);
                return ret;
            });

            AIToolManager.RegisterTool(tool);

            var toolCall = new AIToolCall
            {
                Provider = "test",
                Model = "test-model",
                Body = AIBodyBuilder.Create()
                    .WithTurnId(System.Guid.NewGuid().ToString("N"))
                    .Add(new AIInteractionToolCall
                    {
                        Id = "call-1",
                        Name = "test_echo",
                        Arguments = new JObject { ["input"] = "hello" },
                    })
                    .Build(),
            };

            var result = await AIToolManager.ExecuteTool(toolCall).ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.Equal("echo-result", result.Body?.GetLastText());
        }

        #endregion

        #region ExecuteTool Unknown Tool

#if NET7_WINDOWS
        [Fact(DisplayName = "AIToolManager ExecuteTool unknown tool returns error [Windows]")]
#else
        [Fact(DisplayName = "AIToolManager ExecuteTool unknown tool returns error [Core]")]
#endif
        public async Task ExecuteTool_UnknownTool_ReturnsError()
        {
            AIToolManager.ResetTools();

            var toolCall = new AIToolCall
            {
                Provider = "test",
                Model = "test-model",
                Body = AIBodyBuilder.Create()
                    .WithTurnId(System.Guid.NewGuid().ToString("N"))
                    .Add(new AIInteractionToolCall
                    {
                        Id = "call-1",
                        Name = "unknown_tool",
                        Arguments = new JObject(),
                    })
                    .Build(),
            };

            var result = await AIToolManager.ExecuteTool(toolCall).ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.True(result.Messages.Exists(m => m.Severity == Diagnostics.SHRuntimeMessageSeverity.Error));
        }

        #endregion
    }
}
