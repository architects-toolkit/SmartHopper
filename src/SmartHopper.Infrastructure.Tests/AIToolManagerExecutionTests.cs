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
    using System.Reflection;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AITools;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.Diagnostics;
    using SmartHopper.ProviderSdk.Hosting;
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
            this.ResetTools();

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
            Assert.True(result.Messages.Exists(m => m.Severity == SHRuntimeMessageSeverity.Error));
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
            this.ResetTools();

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

        #region ExecuteTool Null Arguments Normalization

#if NET7_WINDOWS
        [Fact(DisplayName = "AIToolManager ExecuteTool normalizes null arguments for schema with no required properties [Windows]")]
#else
        [Fact(DisplayName = "AIToolManager ExecuteTool normalizes null arguments for schema with no required properties [Core]")]
#endif
        public async Task ExecuteTool_NullArguments_NoRequiredParameters_NormalizesToEmptyJObject()
        {
            this.ResetTools();

            JObject? observedArgs = null;
            var tool = new AITool(
                "test_null_args",
                "Tool with no required parameters",
                "test",
                @"{ ""type"": ""object"", ""properties"": {} }",
                request =>
                {
                    observedArgs = request.GetToolCall().Arguments;
                    request.SkipMetricsValidation = true;
                    var ret = new AIReturn
                    {
                        Request = request,
                    };
                    var body = AIBodyBuilder.Create()
                        .WithTurnId(System.Guid.NewGuid().ToString("N"))
                        .AddText(AIAgent.ToolResult, "ok")
                        .Build();
                    ret.SetBody(body);
                    return Task.FromResult(ret);
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
                        Name = "test_null_args",
                        Arguments = null!,
                    })
                    .Build(),
            };

            var result = await AIToolManager.ExecuteTool(toolCall).ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.Equal("ok", result.Body?.GetLastText());
            Assert.False(result.Messages.Exists(m => m.Severity == SHRuntimeMessageSeverity.Error));
            Assert.NotNull(observedArgs);
            Assert.False(observedArgs!.HasValues);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIToolManager ExecuteTool does not normalize null arguments when required properties are missing [Windows]")]
#else
        [Fact(DisplayName = "AIToolManager ExecuteTool does not normalize null arguments when required properties are missing [Core]")]
#endif
        public async Task ExecuteTool_NullArguments_RequiredParameters_ReturnsValidationError()
        {
            this.ResetTools();

            var tool = new AITool(
                "test_required_args",
                "Tool with required parameters",
                "test",
                @"{ ""type"": ""object"", ""properties"": { ""value"": { ""type"": ""string"" } }, ""required"": [""value""] }",
                _ => Task.FromResult(new AIReturn()));

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
                        Name = "test_required_args",
                        Arguments = null!,
                    })
                    .Build(),
            };

            var result = await AIToolManager.ExecuteTool(toolCall).ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.True(result.Messages.Exists(m => m.Severity == SHRuntimeMessageSeverity.Error));
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
            this.ResetTools();

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
            Assert.True(result.Messages.Exists(m => m.Severity == SHRuntimeMessageSeverity.Error));
        }

        [Fact]
        public async Task ExecuteTool_ToolUnavailableOnSurface_ReturnsErrorWithoutExecuting()
        {
            this.ResetTools();
            var executed = false;
            var tool = new AITool(
                "chat_only",
                "Chat only",
                "Control",
                "{}",
                _ =>
                {
                    executed = true;
                    return Task.FromResult(new AIReturn());
                },
                surfaces: AIToolSurface.Chat);
            AIToolManager.RegisterTool(tool);
            var toolCall = new AIToolCall
            {
                Provider = "test",
                Model = "test-model",
                ToolSurface = AIToolSurface.Mcp,
                InvocationContext = new SmartHopper.Infrastructure.Consent.MutationInvocationContext
                {
                    Surface = AIToolSurface.Mcp,
                },
                Body = AIBodyBuilder.Create()
                    .Add(new AIInteractionToolCall
                    {
                        Id = "call-1",
                        Name = tool.Name,
                        Arguments = new JObject(),
                    })
                    .Build(),
            };

            var result = await AIToolManager.ExecuteTool(toolCall);

            Assert.False(executed);
            Assert.Contains(result.Messages, message => message.Message?.Contains("not available") == true);
        }

        [Fact]
        public async Task ExecuteTool_MutatingTool_CompletesUndoScope()
        {
            this.ResetTools();
            var coordinator = new RecordingUndoCoordinator();
            MutationUndoCoordinator.Current = coordinator;
            try
            {
                var tool = new AIMutatingTool(
                    "mutation",
                    "Mutation",
                    "Test",
                    "{}",
                    call =>
                    {
                        var result = new AIReturn { Request = call };
                        result.SetBody(AIBodyBuilder.Create()
                            .AddToolResult(new JObject { ["success"] = true })
                            .Build());
                        return Task.FromResult(result);
                    });
                AIToolManager.RegisterTool(tool);
                var interaction = new AIInteractionToolCall
                {
                    Id = "mutation-call",
                    Name = tool.Name,
                    Arguments = new JObject(),
                };
                var call = new AIToolCall
                {
                    Provider = "test",
                    Model = "test-model",
                };
                call.FromToolCallInteraction(interaction);

                await AIToolManager.ExecuteTool(call);

                Assert.True(coordinator.Scope.Completed);
            }
            finally
            {
                MutationUndoCoordinator.Current = null;
            }
        }

        private void ResetTools()
        {
            typeof(AIToolManager).GetMethod("ResetTools", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
        }

        private sealed class RecordingUndoCoordinator : IMutationUndoCoordinator
        {
            public RecordingUndoScope Scope { get; } = new RecordingUndoScope();

            public Task<IMutationUndoScope> BeginAsync(string toolName, System.Threading.CancellationToken cancellationToken)
            {
                return Task.FromResult<IMutationUndoScope>(this.Scope);
            }
        }

        private sealed class RecordingUndoScope : IMutationUndoScope
        {
            public bool Completed { get; private set; }

            public Task CompleteAsync(AIReturn result, System.Threading.CancellationToken cancellationToken)
            {
                this.Completed = true;
                return Task.CompletedTask;
            }
        }

        #endregion
    }
}
