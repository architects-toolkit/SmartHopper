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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Interactions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIBodyBuilder"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIBodyBuilderTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Create_Build_ProducesEmptyBody [Windows]")]
#else
        [Fact(DisplayName = "Create_Build_ProducesEmptyBody [Core]")]
#endif
        public void Create_Build_ProducesEmptyBody()
        {
            var body = AIBodyBuilder.Create().Build();

            Assert.Equal(0, body.InteractionsCount);
            Assert.Equal("-*", body.ToolFilter);
            Assert.Equal("-*", body.ContextFilter);
            Assert.Null(body.JsonOutputSchema);
            Assert.False(body.RequiresJsonOutput);
            Assert.Empty(body.InteractionsNew);
            Assert.True(body.AreTurnIdsValid());
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "FromImmutable_PreservesFiltersAndNewMarkers [Windows]")]
#else
        [Fact(DisplayName = "FromImmutable_PreservesFiltersAndNewMarkers [Core]")]
#endif
        public void FromImmutable_PreservesFiltersAndNewMarkers()
        {
            var original = AIBodyBuilder.Create()
                .WithToolFilter("+gh_*")
                .WithContextFilter("+context")
                .WithJsonOutputSchema("{}")
                .AddUser("hello")
                .Build();

            var body = AIBodyBuilder.FromImmutable(original)
                .AddAssistant("reply")
                .Build();

            Assert.Equal("+gh_*", body.ToolFilter);
            Assert.Equal("+context", body.ContextFilter);
            Assert.Equal("{}", body.JsonOutputSchema);
            Assert.True(body.RequiresJsonOutput);
            Assert.Equal(2, body.InteractionsCount);
            Assert.Equal(new[] { 0, 1 }, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Add_DefaultNewness_MarksAsNew [Windows]")]
#else
        [Fact(DisplayName = "Add_DefaultNewness_MarksAsNew [Core]")]
#endif
        public void Add_DefaultNewness_MarksAsNew()
        {
            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "x" })
                .Build();

            Assert.Equal(1, body.InteractionsCount);
            Assert.Single(body.InteractionsNew);
            Assert.Contains(0, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Add_ExplicitNewness_False_DoesNotMark [Windows]")]
#else
        [Fact(DisplayName = "Add_ExplicitNewness_False_DoesNotMark [Core]")]
#endif
        public void Add_ExplicitNewness_False_DoesNotMark()
        {
            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "x" }, false)
                .Build();

            Assert.Empty(body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Add_Null_Ignored [Windows]")]
#else
        [Fact(DisplayName = "Add_Null_Ignored [Core]")]
#endif
        public void Add_Null_Ignored()
        {
            var body = AIBodyBuilder.Create()
                .Add((IAIInteraction)null!)
                .Build();

            Assert.Equal(0, body.InteractionsCount);
            Assert.Empty(body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddRange_DefaultNewness_MarksAll [Windows]")]
#else
        [Fact(DisplayName = "AddRange_DefaultNewness_MarksAll [Core]")]
#endif
        public void AddRange_DefaultNewness_MarksAll()
        {
            var body = AIBodyBuilder.Create()
                .AddRange(new IAIInteraction[]
                {
                    new AIInteractionText { Agent = AIAgent.User, Content = "a" },
                    new AIInteractionText { Agent = AIAgent.Assistant, Content = "b" },
                })
                .Build();

            Assert.Equal(2, body.InteractionsCount);
            Assert.Equal(new[] { 0, 1 }, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddRange_Tuples_RespectsPerItemNewness [Windows]")]
#else
        [Fact(DisplayName = "AddRange_Tuples_RespectsPerItemNewness [Core]")]
#endif
        public void AddRange_Tuples_RespectsPerItemNewness()
        {
            var body = AIBodyBuilder.Create()
                .AddRange(new (IAIInteraction, bool)[]
                {
                    (new AIInteractionText { Agent = AIAgent.User, Content = "a" }, true),
                    (new AIInteractionText { Agent = AIAgent.Assistant, Content = "b" }, false),
                })
                .Build();

            Assert.Equal(2, body.InteractionsCount);
            Assert.Single(body.InteractionsNew);
            Assert.Contains(0, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "AddUser_AddsUserText [Windows]")]
#else
        [Theory(DisplayName = "AddUser_AddsUserText [Core]")]
#endif
        [InlineData("hello", true)]
        [InlineData("hello", false)]
        public void AddUser_AddsUserText(string content, bool markAsNew)
        {
            var body = AIBodyBuilder.Create()
                .AddUser(content, markAsNew)
                .Build();

            Assert.Single(body.Interactions);
            var text = Assert.IsType<AIInteractionText>(body.Interactions[0]);
            Assert.Equal(AIAgent.User, text.Agent);
            Assert.Equal(content, text.Content);

            if (markAsNew)
            {
                Assert.Single(body.InteractionsNew);
                Assert.Contains(0, body.InteractionsNew);
            }
            else
            {
                Assert.Empty(body.InteractionsNew);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddAssistant_AddsAssistantText [Windows]")]
#else
        [Fact(DisplayName = "AddAssistant_AddsAssistantText [Core]")]
#endif
        public void AddAssistant_AddsAssistantText()
        {
            var body = AIBodyBuilder.Create()
                .AddAssistant("reply")
                .Build();

            var text = Assert.IsType<AIInteractionText>(body.Interactions[0]);
            Assert.Equal(AIAgent.Assistant, text.Agent);
            Assert.Equal("reply", text.Content);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddSystem_AddsSystemText [Windows]")]
#else
        [Fact(DisplayName = "AddSystem_AddsSystemText [Core]")]
#endif
        public void AddSystem_AddsSystemText()
        {
            var body = AIBodyBuilder.Create()
                .AddSystem("system prompt")
                .Build();

            var text = Assert.IsType<AIInteractionText>(body.Interactions[0]);
            Assert.Equal(AIAgent.System, text.Agent);
            Assert.Equal("system prompt", text.Content);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddText_WithReasoningAndMetrics [Windows]")]
#else
        [Fact(DisplayName = "AddText_WithReasoningAndMetrics [Core]")]
#endif
        public void AddText_WithReasoningAndMetrics()
        {
            var metrics = new AIMetrics { InputTokensPrompt = 5 };

            var body = AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "answer", metrics, "reasoning chain")
                .Build();

            var text = Assert.IsType<AIInteractionText>(body.Interactions[0]);
            Assert.Equal(AIAgent.Assistant, text.Agent);
            Assert.Equal("answer", text.Content);
            Assert.Equal("reasoning chain", text.Reasoning);
            Assert.Same(metrics, text.Metrics);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddImageRequest_AddsImageGenerationInteraction [Windows]")]
#else
        [Fact(DisplayName = "AddImageRequest_AddsImageGenerationInteraction [Core]")]
#endif
        public void AddImageRequest_AddsImageGenerationInteraction()
        {
            var body = AIBodyBuilder.Create()
                .AddImageRequest("a red cube", "1024x1024", "hd", "vivid", "1:1")
                .Build();

            var img = Assert.IsType<AIInteractionImage>(body.Interactions[0]);
            Assert.Equal(AIAgent.User, img.Agent);
            Assert.Equal("a red cube", img.OriginalPrompt);
            Assert.Equal("1024x1024", img.ImageSize);
            Assert.Equal("hd", img.ImageQuality);
            Assert.Equal("vivid", img.ImageStyle);
            Assert.Equal("1:1", img.AspectRatio);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddImageInput_WithString_AddsVisionInput [Windows]")]
#else
        [Fact(DisplayName = "AddImageInput_WithString_AddsVisionInput [Core]")]
#endif
        public void AddImageInput_WithString_AddsVisionInput()
        {
#pragma warning disable CA2234
            var body = AIBodyBuilder.Create()
                .AddImageInput("https://example.com/image.png")
                .Build();
#pragma warning restore CA2234

            var img = Assert.IsType<AIInteractionImage>(body.Interactions[0]);
            Assert.Equal(new Uri("https://example.com/image.png"), img.ImageUrl);
            Assert.Null(img.ImageData);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddImageInput_WithUri_AddsVisionInput [Windows]")]
#else
        [Fact(DisplayName = "AddImageInput_WithUri_AddsVisionInput [Core]")]
#endif
        public void AddImageInput_WithUri_AddsVisionInput()
        {
            var body = AIBodyBuilder.Create()
                .AddImageInput(new Uri("https://example.com/image.png"))
                .Build();

            var img = Assert.IsType<AIInteractionImage>(body.Interactions[0]);
            Assert.Equal(new Uri("https://example.com/image.png"), img.ImageUrl);
            Assert.Null(img.ImageData);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddImageInputFromBase64_AddsVisionInput [Windows]")]
#else
        [Fact(DisplayName = "AddImageInputFromBase64_AddsVisionInput [Core]")]
#endif
        public void AddImageInputFromBase64_AddsVisionInput()
        {
            var body = AIBodyBuilder.Create()
                .AddImageInputFromBase64("abc123", "image/jpeg")
                .Build();

            var img = Assert.IsType<AIInteractionImage>(body.Interactions[0]);
            Assert.Equal("abc123", img.ImageData);
            Assert.Equal("image/jpeg", img.MimeType);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddToolCall_AddsToolCall [Windows]")]
#else
        [Fact(DisplayName = "AddToolCall_AddsToolCall [Core]")]
#endif
        public void AddToolCall_AddsToolCall()
        {
            var args = new JObject(new JProperty("x", 1));

            var body = AIBodyBuilder.Create()
                .AddToolCall("call-1", "test_tool", args)
                .Build();

            var tc = Assert.IsType<AIInteractionToolCall>(body.Interactions[0]);
            Assert.Equal(AIAgent.ToolCall, tc.Agent);
            Assert.Equal("call-1", tc.Id);
            Assert.Equal("test_tool", tc.Name);
            Assert.Same(args, tc.Arguments);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddToolResult_AddsToolResult [Windows]")]
#else
        [Fact(DisplayName = "AddToolResult_AddsToolResult [Core]")]
#endif
        public void AddToolResult_AddsToolResult()
        {
            var result = new JObject(new JProperty("ok", true));
            var messages = new List<SHRuntimeMessage>
            {
                new SHRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Provider, SHMessageCode.Unknown, "note"),
            };
            var metrics = new AIMetrics { InputTokensPrompt = 10 };

            var body = AIBodyBuilder.Create()
                .AddToolResult(result, "call-1", "test_tool", metrics, messages)
                .Build();

            var tr = Assert.IsType<AIInteractionToolResult>(body.Interactions[0]);
            Assert.Equal(AIAgent.ToolResult, tr.Agent);
            Assert.Equal("call-1", tr.Id);
            Assert.Equal("test_tool", tr.Name);
            Assert.Same(result, tr.Result);
            Assert.Same(messages, tr.Messages);
            Assert.Same(metrics, tr.Metrics);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddError_AddsErrorDiagnostic [Windows]")]
#else
        [Fact(DisplayName = "AddError_AddsErrorDiagnostic [Core]")]
#endif
        public void AddError_AddsErrorDiagnostic()
        {
            var body = AIBodyBuilder.Create()
                .AddError("boom")
                .Build();

            var diag = Assert.IsType<AIInteractionRuntimeMessage>(body.Interactions[0]);
            Assert.Equal(SHRuntimeMessageSeverity.Error, diag.Severity);
            Assert.Equal("boom", diag.Content);
            Assert.True(diag.Surfaceable);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddWarning_AddsWarningDiagnostic [Windows]")]
#else
        [Fact(DisplayName = "AddWarning_AddsWarningDiagnostic [Core]")]
#endif
        public void AddWarning_AddsWarningDiagnostic()
        {
            var body = AIBodyBuilder.Create()
                .AddWarning("watch out")
                .Build();

            var diag = Assert.IsType<AIInteractionRuntimeMessage>(body.Interactions[0]);
            Assert.Equal(SHRuntimeMessageSeverity.Warning, diag.Severity);
            Assert.Equal("watch out", diag.Content);
            Assert.True(diag.Surfaceable);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddInfo_AddsInfoDiagnostic [Windows]")]
#else
        [Fact(DisplayName = "AddInfo_AddsInfoDiagnostic [Core]")]
#endif
        public void AddInfo_AddsInfoDiagnostic()
        {
            var body = AIBodyBuilder.Create()
                .AddInfo("noted")
                .Build();

            var diag = Assert.IsType<AIInteractionRuntimeMessage>(body.Interactions[0]);
            Assert.Equal(SHRuntimeMessageSeverity.Info, diag.Severity);
            Assert.Equal("noted", diag.Content);
            Assert.True(diag.Surfaceable);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AddDebug_AddsDebugDiagnosticNonSurfaceable [Windows]")]
#else
        [Fact(DisplayName = "AddDebug_AddsDebugDiagnosticNonSurfaceable [Core]")]
#endif
        public void AddDebug_AddsDebugDiagnosticNonSurfaceable()
        {
            var body = AIBodyBuilder.Create()
                .AddDebug("trace")
                .Build();

            var diag = Assert.IsType<AIInteractionRuntimeMessage>(body.Interactions[0]);
            Assert.Equal(SHRuntimeMessageSeverity.Debug, diag.Severity);
            Assert.Equal("trace", diag.Content);
            Assert.False(diag.Surfaceable);
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Diagnostics_RespectNewness [Windows]")]
#else
        [Theory(DisplayName = "Diagnostics_RespectNewness [Core]")]
#endif
        [InlineData(true)]
        [InlineData(false)]
        public void Diagnostics_RespectNewness(bool markAsNew)
        {
            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddError("err", markAsNew)
                .Build();

            if (markAsNew)
            {
                Assert.Single(body.InteractionsNew);
                Assert.Contains(0, body.InteractionsNew);
            }
            else
            {
                Assert.Empty(body.InteractionsNew);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "WithToolFilter_WithContextFilter_WithJsonOutputSchema_PreservedOnBuild [Windows]")]
#else
        [Fact(DisplayName = "WithToolFilter_WithContextFilter_WithJsonOutputSchema_PreservedOnBuild [Core]")]
#endif
        public void WithToolFilter_WithContextFilter_WithJsonOutputSchema_PreservedOnBuild()
        {
            var body = AIBodyBuilder.Create()
                .WithToolFilter("+gh_*")
                .WithContextFilter("+ctx")
                .WithJsonOutputSchema("{\"type\":\"object\"}")
                .WithToolFilter(null!)
                .WithContextFilter(null!)
                .WithJsonOutputSchema(null!)
                .AddUser("hi")
                .Build();

            Assert.Equal("+gh_*", body.ToolFilter);
            Assert.Equal("+ctx", body.ContextFilter);
            Assert.Equal("{\"type\":\"object\"}", body.JsonOutputSchema);
            Assert.True(body.RequiresJsonOutput);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "WithTurnId_AssignedToInteractionsWithoutTurnId [Windows]")]
#else
        [Fact(DisplayName = "WithTurnId_AssignedToInteractionsWithoutTurnId [Core]")]
#endif
        public void WithTurnId_AssignedToInteractionsWithoutTurnId()
        {
            var body = AIBodyBuilder.Create()
                .WithTurnId("turn-shared")
                .AddUser("u1")
                .AddAssistant("a1")
                .Build();

            Assert.True(body.AreTurnIdsValid());
            Assert.All(body.Interactions, i => Assert.Equal("turn-shared", i.TurnId));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "WithTurnId_PreservesPreExistingTurnId [Windows]")]
#else
        [Fact(DisplayName = "WithTurnId_PreservesPreExistingTurnId [Core]")]
#endif
        public void WithTurnId_PreservesPreExistingTurnId()
        {
            var existing = new AIInteractionText { Agent = AIAgent.User, Content = "u", TurnId = "existing" };

            var body = AIBodyBuilder.Create()
                .WithTurnId("default")
                .Add(existing)
                .Build();

            Assert.Equal("existing", body.Interactions[0].TurnId);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AsHistory_AsNew_ControlDefaultNewness [Windows]")]
#else
        [Fact(DisplayName = "AsHistory_AsNew_ControlDefaultNewness [Core]")]
#endif
        public void AsHistory_AsNew_ControlDefaultNewness()
        {
            var historyBody = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("u")
                .Build();

            Assert.Empty(historyBody.InteractionsNew);

            var newBody = AIBodyBuilder.Create()
                .AsNew()
                .AddUser("u")
                .Build();

            Assert.Single(newBody.InteractionsNew);
            Assert.Contains(0, newBody.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ClearNewMarkers_ClearsAndAllowsFurtherMarking [Windows]")]
#else
        [Fact(DisplayName = "ClearNewMarkers_ClearsAndAllowsFurtherMarking [Core]")]
#endif
        public void ClearNewMarkers_ClearsAndAllowsFurtherMarking()
        {
            var body = AIBodyBuilder.Create()
                .AddUser("u")
                .ClearNewMarkers()
                .AddAssistant("a")
                .Build();

            Assert.Single(body.InteractionsNew);
            Assert.Contains(1, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "MarkLastAsNew_MarksLastInteraction [Windows]")]
#else
        [Fact(DisplayName = "MarkLastAsNew_MarksLastInteraction [Core]")]
#endif
        public void MarkLastAsNew_MarksLastInteraction()
        {
            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("u")
                .AddAssistant("a")
                .MarkLastAsNew()
                .Build();

            Assert.Single(body.InteractionsNew);
            Assert.Contains(1, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "MarkLastNAsNew_MarksLastN [Windows]")]
#else
        [Fact(DisplayName = "MarkLastNAsNew_MarksLastN [Core]")]
#endif
        public void MarkLastNAsNew_MarksLastN()
        {
            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("u")
                .AddAssistant("a")
                .AddSystem("s")
                .MarkLastNAsNew(2)
                .Build();

            Assert.Equal(new[] { 1, 2 }, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "MarkIndicesAsNew_MarksProvidedIndices [Windows]")]
#else
        [Fact(DisplayName = "MarkIndicesAsNew_MarksProvidedIndices [Core]")]
#endif
        public void MarkIndicesAsNew_MarksProvidedIndices()
        {
            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("u")
                .AddAssistant("a")
                .AddSystem("s")
                .MarkIndicesAsNew(new[] { 0, 2 })
                .Build();

            Assert.Equal(new[] { 0, 2 }, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ReplaceLast_DefaultNewness_ReplacesAndMarks [Windows]")]
#else
        [Fact(DisplayName = "ReplaceLast_DefaultNewness_ReplacesAndMarks [Core]")]
#endif
        public void ReplaceLast_DefaultNewness_ReplacesAndMarks()
        {
            var body = AIBodyBuilder.Create()
                .AddUser("a")
                .AddAssistant("b")
                .ReplaceLast(new AIInteractionText { Agent = AIAgent.Assistant, Content = "c" })
                .Build();

            Assert.Equal(2, body.InteractionsCount);
            var last = Assert.IsType<AIInteractionText>(body.Interactions[1]);
            Assert.Equal("c", last.Content);
            Assert.Equal(new[] { 0, 1 }, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ReplaceLast_ExplicitNewness_False_DoesNotMark [Windows]")]
#else
        [Fact(DisplayName = "ReplaceLast_ExplicitNewness_False_DoesNotMark [Core]")]
#endif
        public void ReplaceLast_ExplicitNewness_False_DoesNotMark()
        {
            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("a")
                .AddAssistant("b")
                .ReplaceLast(new AIInteractionText { Agent = AIAgent.Assistant, Content = "c" }, false)
                .Build();

            Assert.Equal(2, body.InteractionsCount);
            Assert.Empty(body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ReplaceLast_WhenEmpty_TreatsAsAdd [Windows]")]
#else
        [Fact(DisplayName = "ReplaceLast_WhenEmpty_TreatsAsAdd [Core]")]
#endif
        public void ReplaceLast_WhenEmpty_TreatsAsAdd()
        {
            var body = AIBodyBuilder.Create()
                .ReplaceLast(new AIInteractionText { Agent = AIAgent.User, Content = "x" })
                .Build();

            Assert.Single(body.Interactions);
            Assert.Single(body.InteractionsNew);
            Assert.Contains(0, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ReplaceLastRange_Slice_WithDefaultNewness [Windows]")]
#else
        [Fact(DisplayName = "ReplaceLastRange_Slice_WithDefaultNewness [Core]")]
#endif
        public void ReplaceLastRange_Slice_WithDefaultNewness()
        {
            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("a")
                .AddAssistant("b")
                .AddSystem("c")
                .AsNew()
                .ReplaceLastRange(new List<IAIInteraction>
                {
                    new AIInteractionText { Agent = AIAgent.Assistant, Content = "y" },
                    new AIInteractionText { Agent = AIAgent.Assistant, Content = "z" },
                })
                .Build();

            Assert.Equal(3, body.InteractionsCount);
            Assert.IsType<AIInteractionText>(body.Interactions[0]);
            Assert.Equal("y", ((AIInteractionText)body.Interactions[1]).Content);
            Assert.Equal("z", ((AIInteractionText)body.Interactions[2]).Content);
            Assert.Equal(new[] { 1, 2 }, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ReplaceLastRange_Slice_ExplicitNewness [Windows]")]
#else
        [Fact(DisplayName = "ReplaceLastRange_Slice_ExplicitNewness [Core]")]
#endif
        public void ReplaceLastRange_Slice_ExplicitNewness()
        {
            var replacements = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "y" },
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "z" },
            };

            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("a")
                .AddAssistant("b")
                .AddSystem("c")
                .ReplaceLastRange(replacements, false)
                .Build();

            Assert.Equal(3, body.InteractionsCount);
            Assert.Empty(body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ReplaceLastRange_ResetWhenReplacingMoreThanExisting [Windows]")]
#else
        [Fact(DisplayName = "ReplaceLastRange_ResetWhenReplacingMoreThanExisting [Core]")]
#endif
        public void ReplaceLastRange_ResetWhenReplacingMoreThanExisting()
        {
            var replacements = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "x" },
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "y" },
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "z" },
            };

            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("a")
                .AddAssistant("b")
                .ReplaceLastRange(replacements, true)
                .Build();

            Assert.Equal(3, body.InteractionsCount);
            Assert.Equal(new[] { 0, 1, 2 }, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ReplaceLastRange_TupleOverload_RespectsPerItemNewness [Windows]")]
#else
        [Fact(DisplayName = "ReplaceLastRange_TupleOverload_RespectsPerItemNewness [Core]")]
#endif
        public void ReplaceLastRange_TupleOverload_RespectsPerItemNewness()
        {
            var replacements = new List<(IAIInteraction, bool)>
            {
                (new AIInteractionText { Agent = AIAgent.Assistant, Content = "y" }, true),
                (new AIInteractionText { Agent = AIAgent.Assistant, Content = "z" }, false),
            };

            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("a")
                .AddAssistant("b")
                .AddSystem("c")
                .ReplaceLastRange(replacements)
                .Build();

            Assert.Equal(3, body.InteractionsCount);
            Assert.Single(body.InteractionsNew);
            Assert.Contains(1, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ReplaceLastRange_EmptyList_DoesNothing [Windows]")]
#else
        [Fact(DisplayName = "ReplaceLastRange_EmptyList_DoesNothing [Core]")]
#endif
        public void ReplaceLastRange_EmptyList_DoesNothing()
        {
            var body = AIBodyBuilder.Create()
                .AddUser("u")
                .ReplaceLastRange(new List<IAIInteraction>())
                .Build();

            Assert.Single(body.Interactions);
            Assert.Equal("u", ((AIInteractionText)body.Interactions[0]).Content);
            Assert.Single(body.InteractionsNew);
            Assert.Contains(0, body.InteractionsNew);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Build_ClampsAndDeduplicatesNewMarkers [Windows]")]
#else
        [Fact(DisplayName = "Build_ClampsAndDeduplicatesNewMarkers [Core]")]
#endif
        public void Build_ClampsAndDeduplicatesNewMarkers()
        {
            var body = AIBodyBuilder.Create()
                .AsHistory()
                .AddUser("u")
                .MarkLastAsNew()
                .MarkLastAsNew()
                .MarkIndicesAsNew(new[] { 0, 0, 99 })
                .Build();

            Assert.Single(body.InteractionsNew);
            Assert.Contains(0, body.InteractionsNew);
        }
    }
}
