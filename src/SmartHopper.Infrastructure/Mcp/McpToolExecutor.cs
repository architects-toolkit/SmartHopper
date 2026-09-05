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

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Helper that executes a SmartHopper AITool by name through <see cref="AIToolCall"/>
    /// and returns the raw tool result JObject.
    /// </summary>
    internal static class McpToolExecutor
    {
        /// <summary>
        /// Executes the named tool with the supplied arguments.
        /// </summary>
        /// <param name="toolName">Tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tool result payload, or <c>null</c> if the tool returned no result.</returns>
        [SuppressMessage("Design", "CA1031", Justification = "Resource/prompt providers must be resilient; tool failures are surfaced as fallback text.")]
        public static async Task<JObject?> ExecuteAsync(string toolName, JObject arguments, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return null;
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
                result = await toolCall.Exec(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["error"] = $"Tool '{toolName}' threw an exception: {ex.Message}",
                };
            }

            var lastToolResult = result?.Body?.Interactions?
                .OfType<AIInteractionToolResult>()
                .LastOrDefault();

            if (lastToolResult?.Result != null)
            {
                return lastToolResult.Result;
            }

            return new JObject
            {
                ["error"] = $"Tool '{toolName}' returned no result.",
            };
        }
    }
}
