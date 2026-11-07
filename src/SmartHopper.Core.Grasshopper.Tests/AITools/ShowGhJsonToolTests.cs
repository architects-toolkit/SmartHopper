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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.Consent;
using SmartHopper.Infrastructure.Interaction;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    /// <summary>
    /// Tests the show_ghjson tool's metadata, argument validation, schema-error
    /// reporting and presenter handoff without Rhino runtime activation. Canvas
    /// placement itself is Grasshopper-bound and out of scope here.
    /// </summary>
    public class ShowGhJsonToolTests
    {
        private const string ValidDocument =
            "{ \"components\": [ { \"name\": \"Panel\", \"id\": 1, \"pivot\": { \"x\": 0, \"y\": 0 } } ] }";

        private const string InvalidDocument =
            "{ \"export\": true, \"wires\": [] }";

        [Fact]
        public void GetTools_ExposesShowGhJsonAsReadOnlyDisplay()
        {
            var tool = new show_ghjson().GetTools().Single();

            Assert.Equal("show_ghjson", tool.Name);
            Assert.Equal("GhJSON.Tools", tool.Category);
            Assert.False(tool.MutatesCanvas);
            Assert.True(tool.Annotations.ReadOnlyHint == true);
        }

        [Fact]
        public async Task Execute_RejectsMissingGhjson()
        {
            var call = CreateCall(new JObject());
            var tool = new show_ghjson().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "required");
        }

        [Fact]
        public async Task Execute_ReturnsStructuredErrorsForInvalidGhjson()
        {
            var call = CreateCall(new JObject { ["ghjson"] = InvalidDocument });
            var tool = new show_ghjson().GetTools().Single();
            var result = await tool.Execute(call);

            var payload = ToolResult(result);
            Assert.Equal("invalid_ghjson", payload["error"]?.ToString());
            Assert.False(payload["valid"]?.Value<bool>() ?? true);
            var errors = payload["errors"] as JArray;
            Assert.NotNull(errors);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public async Task Execute_MarksDocumentValidWithoutPresenter()
        {
            var call = CreateCall(new JObject { ["ghjson"] = ValidDocument });
            var tool = new show_ghjson().GetTools().Single();
            var result = await tool.Execute(call);

            var payload = ToolResult(result);
            Assert.True(payload["valid"]?.Value<bool>() ?? false);
            Assert.False(payload["displayed"]?.Value<bool>() ?? true);
            Assert.NotNull(payload["artifactId"]?.ToString());
        }

        [Fact]
        public async Task Execute_PresentsArtifactThroughInvocationContext()
        {
            var presenter = new RecordingPresenter();
            var call = CreateCall(
                new JObject { ["ghjson"] = ValidDocument, ["title"] = "My grid" },
                presenter);
            var tool = new show_ghjson().GetTools().Single();
            var result = await tool.Execute(call);

            var artifact = presenter.Shown;
            Assert.NotNull(artifact);
            Assert.True(artifact.Valid);
            Assert.Equal("My grid", artifact.Title);
            Assert.Equal(1, artifact.Components);
            Assert.Empty(artifact.Errors);

            var payload = ToolResult(result);
            Assert.True(payload["displayed"]?.Value<bool>() ?? false);
            Assert.Equal(artifact.Id, payload["artifactId"]?.ToString());
        }

        [Fact]
        public async Task Execute_PresentsInvalidArtifactWithoutPlacementData()
        {
            var presenter = new RecordingPresenter();
            var call = CreateCall(new JObject { ["ghjson"] = InvalidDocument }, presenter);
            var tool = new show_ghjson().GetTools().Single();
            await tool.Execute(call);

            var artifact = presenter.Shown;
            Assert.NotNull(artifact);
            Assert.False(artifact.Valid);
            Assert.NotEmpty(artifact.Errors);
        }

        private static AIToolCall CreateCall(JObject arguments, IGhJsonArtifactPresenter? presenter = null)
        {
            var interaction = new AIInteractionToolCall
            {
                Id = "show-ghjson-call",
                Name = "show_ghjson",
                Arguments = arguments,
            };
            var call = new AIToolCall
            {
                ToolSurface = AIToolSurface.Chat,
                InvocationContext = new MutationInvocationContext
                {
                    Surface = AIToolSurface.Chat,
                    GhJsonArtifactPresenter = presenter,
                },
            };
            call.FromToolCallInteraction(interaction);
            return call;
        }

        private static JObject ToolResult(AIReturn result)
        {
            var interaction = result.Body?.GetLastInteraction() as AIInteractionToolResult;
            Assert.NotNull(interaction);
            return interaction!.Result as JObject ?? new JObject();
        }

        private static void AssertToolError(AIReturn result, string fragment)
        {
            Assert.Contains(result.Messages, message => message.Message.Contains(fragment));
        }

        private sealed class RecordingPresenter : IGhJsonArtifactPresenter
        {
            public GhJsonArtifact? Shown { get; private set; }

            public Task ShowAsync(GhJsonArtifact artifact, CancellationToken cancellationToken)
            {
                this.Shown = artifact;
                return Task.CompletedTask;
            }
        }
    }
}
