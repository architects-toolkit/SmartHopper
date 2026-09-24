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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.Consent;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    /// <summary>
    /// Tests the canvas_point tool's metadata and argument validation without Rhino
    /// runtime activation. The canvas display path itself is Grasshopper-bound and
    /// therefore covered only up to the validation boundary.
    /// </summary>
    public class CanvasPointToolTests
    {
        [Fact]
        public void GetTools_ExposesCanvasPointAsReadOnlyViewControl()
        {
            var tool = new canvas_point().GetTools().Single();

            Assert.Equal("canvas_point", tool.Name);
            Assert.Equal("ViewControl", tool.Category);
            Assert.False(tool.MutatesCanvas);
            Assert.True(tool.Annotations.ReadOnlyHint == true);
        }

        [Fact]
        public async Task Execute_RejectsMissingTarget()
        {
            var call = CreateCall(new JObject
            {
                ["message"] = "Look here",
            });

            var tool = new canvas_point().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "at least one target");
        }

        [Fact]
        public async Task Execute_RejectsInvalidGuidsOnly()
        {
            var call = CreateCall(new JObject
            {
                ["message"] = "Look here",
                ["guids"] = new JArray("not-a-guid", "also-not-a-guid"),
            });

            var tool = new canvas_point().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "at least one target");
        }

        [Fact]
        public async Task Execute_RejectsNonPositiveRegion()
        {
            var call = CreateCall(new JObject
            {
                ["message"] = "Look here",
                ["region"] = new JObject
                {
                    ["x"] = 0, ["y"] = 0, ["width"] = -10, ["height"] = 50,
                },
            });

            var tool = new canvas_point().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "at least one target");
        }

        [Fact]
        public async Task Execute_RejectsPartialRegion()
        {
            var call = CreateCall(new JObject
            {
                ["message"] = "Look here",
                ["region"] = new JObject
                {
                    ["x"] = 0, ["y"] = 0,
                },
            });

            var tool = new canvas_point().GetTools().Single();
            var result = await tool.Execute(call);

            AssertToolError(result, "at least one target");
        }

        private static AIToolCall CreateCall(JObject arguments)
        {
            var interaction = new AIInteractionToolCall
            {
                Id = "canvas-point-call",
                Name = "canvas_point",
                Arguments = arguments,
            };
            var call = new AIToolCall
            {
                ToolSurface = AIToolSurface.Chat,
                InvocationContext = new MutationInvocationContext
                {
                    Surface = AIToolSurface.Chat,
                },
            };
            call.FromToolCallInteraction(interaction);
            return call;
        }

        private static void AssertToolError(AIReturn result, string fragment)
        {
            Assert.Contains(result.Messages, message => message.Message.Contains(fragment));
        }
    }
}
