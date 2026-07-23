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

/*
 * The reflection-based tool-discovery pattern is structurally adapted from Cordyceps
 * (https://github.com/brookstalley/cordyceps, McpServer.cs). SmartHopper does not
 * use [McpServerTool] reflection: tools are already discoverable through
 * AIToolManager, so the adapter projects that catalog into MCP descriptors instead.
 * Copyright (c) 2026 Brooks Talley. Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Bridges SmartHopper's <see cref="AIToolManager"/> catalog to the Model Context
    /// Protocol's <c>tools/list</c> and <c>tools/call</c> surface.
    /// </summary>
    /// <remarks>
    /// All schema-layer concerns (GhJSON marshalling, validation, fix-up, placement)
    /// continue to live inside the tools themselves and the upstream
    /// <c>architects-toolkit/ghjson-dotnet</c> library. This adapter only translates
    /// between the MCP envelope and <see cref="AIToolCall"/> / <see cref="AIReturn"/>.
    /// </remarks>
    public sealed class AIToolMcpAdapter
    {
        private readonly McpServerOptions options;
        private readonly Func<IReadOnlyDictionary<string, AITool>> toolSource;
        private readonly Func<AIToolCall, Task<AIReturn>> executor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIToolMcpAdapter"/> class.
        /// </summary>
        public AIToolMcpAdapter(McpServerOptions options)
            : this(options, () => AIToolManager.GetTools(), call => call.Exec())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIToolMcpAdapter"/> class with
        /// injectable tool source and executor. Used by tests and embedding scenarios
        /// that want to substitute the catalog or execution pipeline.
        /// </summary>
        public AIToolMcpAdapter(
            McpServerOptions options,
            Func<IReadOnlyDictionary<string, AITool>> toolSource,
            Func<AIToolCall, Task<AIReturn>> executor)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.toolSource = toolSource ?? throw new ArgumentNullException(nameof(toolSource));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>
        /// Builds the list of MCP tool descriptors to expose, applying the configured
        /// allow-list and per-tool mutability filter.
        /// </summary>
        public IReadOnlyList<McpToolDescriptor> BuildDescriptors()
        {
            var allTools = this.toolSource();
            var descriptors = new List<McpToolDescriptor>();
            foreach (var pair in allTools)
            {
                var tool = pair.Value;
                if (!this.IsExposed(tool.Name))
                {
                    continue;
                }

                JObject inputSchema = ParseSchema(tool.ParametersSchema);
                JObject outputSchema = ParseSchema(tool.OutputSchema);
                descriptors.Add(new McpToolDescriptor(
                    tool.Name,
                    tool.RichDescription,
                    inputSchema,
                    tool.Tags,
                    outputSchema,
                    tool.Annotations));
            }

            descriptors.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return descriptors;
        }

        /// <summary>
        /// Returns whether the named tool is currently exposed (allow-list + per-tool mutability).
        /// </summary>
        public bool IsExposed(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return false;
            }

            var tool = this.toolSource().TryGetValue(toolName, out var resolvedTool) ? resolvedTool : null;
            if (tool == null)
            {
                return false;
            }

            if (!tool.Enabled)
            {
                return false;
            }

            if (this.options.EnabledTools != null && this.options.EnabledTools.Count > 0)
            {
                return this.options.EnabledTools.Any(t =>
                    string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase));
            }

            if (!this.options.ExposeMutatingTools && tool.MutatesCanvas)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Executes a tool via <see cref="AIToolCall.Exec(System.Threading.CancellationToken)"/>
        /// and returns the JSON payload to embed in the MCP <c>tools/call</c> response.
        /// </summary>
        /// <param name="toolName">Tool name.</param>
        /// <param name="arguments">JSON object containing tool arguments.</param>
        public async Task<McpToolCallResult> ExecuteAsync(string toolName, JObject? arguments)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return McpToolCallResult.Error("Tool name is required");
            }

            if (!this.IsExposed(toolName))
            {
                return McpToolCallResult.Error($"Tool '{toolName}' is not exposed via MCP");
            }

            var tools = this.toolSource();
            if (!tools.ContainsKey(toolName))
            {
                return McpToolCallResult.Error($"Tool '{toolName}' is not registered");
            }

            var interaction = new AIInteractionToolCall
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = toolName,
                Arguments = arguments ?? new JObject(),
            };

            var toolCall = new AIToolCall { SkipMetricsValidation = true };
            toolCall.FromToolCallInteraction(interaction);

            AIReturn result;
            try
            {
                result = await this.executor(toolCall).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return McpToolCallResult.Error($"Tool '{toolName}' threw an exception: {ex.Message}");
            }

            return BuildResult(toolName, result);
        }

        private static JObject ParseSchema(string parametersSchema)
        {
            if (string.IsNullOrWhiteSpace(parametersSchema))
            {
                return new JObject { ["type"] = "object" };
            }

            try
            {
                var parsed = JToken.Parse(parametersSchema);
                if (parsed is JObject obj)
                {
                    return obj;
                }
            }
            catch (JsonReaderException)
            {
                // Fall through to a defensive default below; surfacing this loudly
                // is the tool author's responsibility.
            }

            return new JObject { ["type"] = "object" };
        }

        private static McpToolCallResult BuildResult(string toolName, AIReturn? result)
        {
            if (result == null)
            {
                return McpToolCallResult.Error($"Tool '{toolName}' returned no result");
            }

            // Prefer the tool result interaction when present: a successful tool
            // run always appends an AIInteractionToolResult to the body.
            var lastToolResult = result.Body?.Interactions?
                .OfType<AIInteractionToolResult>()
                .LastOrDefault();
            if (lastToolResult?.Result != null)
            {
                return McpToolCallResult.Ok(lastToolResult.Result);
            }

            // Otherwise surface any explicit error. Previously Origin.Return and
            // Origin.Validation were ignored, which caused AI tools that call
            // CreateError (e.g., from gh_get) to silently return an empty object.
            var firstError = result.Messages?
                .FirstOrDefault(m => m?.Severity == SHRuntimeMessageSeverity.Error);
            if (firstError != null)
            {
                return McpToolCallResult.Error(firstError.Message ?? $"Tool '{toolName}' failed");
            }

            return McpToolCallResult.Ok(new JObject());
        }
    }
}
