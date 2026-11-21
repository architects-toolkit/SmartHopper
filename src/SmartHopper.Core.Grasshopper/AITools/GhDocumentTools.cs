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
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Shared logic for the gh_document_* AI tools: output shape, tool execution wrapper and document lookup.
    /// </summary>
    internal static class GhDocumentTools
    {
        /// <summary>
        /// JSON schema of a single document description as returned by <see cref="GhDocumentAccess.Describe"/>.
        /// </summary>
        public const string DocumentOutputSchema = @"{ ""type"": ""object"", ""properties"": { ""documentId"": { ""type"": ""string"" }, ""name"": { ""type"": ""string"" }, ""filePath"": { ""type"": [""string"", ""null""] }, ""isActive"": { ""type"": ""boolean"" }, ""isModified"": { ""type"": ""boolean"" }, ""objectCount"": { ""type"": ""integer"" }, ""hostsMcpServer"": { ""type"": ""boolean"" } } }";

        /// <summary>
        /// Warning appended to the descriptions of tools that change the active document.
        /// The MCP server lives inside a document; unloading its host stops the server.
        /// </summary>
        public const string McpDisconnectNote = " MCP clients: if the document you are leaving hosts the enabled SmartHopper MCP Server component, activating a different document unloads it, stops this server, and drops your session before the result can be returned — refused on MCP unless allowDisconnect is true.";

        /// <summary>
        /// JSON schema fragment for the <c>allowDisconnect</c> argument accepted by the
        /// document-activation tools.
        /// </summary>
        public const string AllowDisconnectSchema = @"""allowDisconnect"": { ""type"": ""boolean"", ""default"": false, ""description"": ""MCP only: proceed even when this call unloads the document hosting the enabled SmartHopper MCP Server component, stopping the server and disconnecting the session before the result can be returned."" }";

        /// <summary>
        /// Surfaces for document tools that change which document is active or create/open documents:
        /// interactive only (no Batch components).
        /// </summary>
        public const AIToolSurface InteractiveSurfaces = AIToolSurface.Chat | AIToolSurface.Direct | AIToolSurface.Mcp;

        /// <summary>
        /// Executes <paramref name="action"/> and wraps the JSON result or the error in an AIReturn.
        /// </summary>
        /// <param name="toolCall">The tool call.</param>
        /// <param name="action">Builds the tool result from the call arguments.</param>
        /// <returns>The tool result.</returns>
        public static Task<AIReturn> Run(AIToolCall toolCall, Func<JObject, JObject> action)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                toolCall.SkipMetricsValidation = true;
                var args = toolCall.GetToolCall().GetArgumentsOrEmpty();
                var result = action(args);
                var body = AIBodyBuilder.Create()
                    .AddToolResult(result)
                    .Build();
                output.CreateSuccess(body, toolCall);
            }
            catch (Exception ex)
            {
                output.CreateError(ex is InvalidOperationException ? ex.Message : $"Error: {ex.Message}");
            }

            return Task.FromResult(output);
        }

        /// <summary>
        /// Executes an async <paramref name="action"/> and wraps the JSON result or the error in an AIReturn.
        /// </summary>
        /// <param name="toolCall">The tool call.</param>
        /// <param name="action">Builds the tool result from the call arguments.</param>
        /// <returns>The tool result.</returns>
        public static async Task<AIReturn> RunAsync(AIToolCall toolCall, Func<JObject, Task<JObject>> action)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                toolCall.SkipMetricsValidation = true;
                var args = toolCall.GetToolCall().GetArgumentsOrEmpty();
                var result = await action(args).ConfigureAwait(false);
                var body = AIBodyBuilder.Create()
                    .AddToolResult(result)
                    .Build();
                output.CreateSuccess(body, toolCall);
            }
            catch (Exception ex)
            {
                output.CreateError(ex is InvalidOperationException ? ex.Message : $"Error: {ex.Message}");
            }

            return output;
        }

        /// <summary>
        /// Finds an open document by <paramref name="documentId"/> or throws a model-facing error.
        /// </summary>
        /// <param name="documentId">The document id.</param>
        /// <returns>The document.</returns>
        public static GH_Document RequireDocument(string? documentId)
        {
            return GhDocumentAccess.FindById(documentId)
                ?? throw new InvalidOperationException($"No open document with documentId '{documentId}'. Call gh_document_list to get valid ids.");
        }

        /// <summary>
        /// Throws when an MCP-surface call would change the active document away from
        /// the document hosting the last enabled MCP server component: the unload
        /// releases the component's hold, stops the server mid-request, and the
        /// session dies before this call's result can be delivered. Pass
        /// <c>allowDisconnect: true</c> to proceed anyway; calls on other surfaces
        /// and activations that keep the current document are unaffected, as are
        /// documents whose MCP server is hosted elsewhere too.
        /// </summary>
        /// <param name="toolCall">The tool call being executed.</param>
        /// <param name="args">The parsed tool arguments.</param>
        /// <param name="target">The document that would become active; <c>null</c> for a new document.</param>
        public static void ThrowIfDisconnectsMcp(AIToolCall toolCall, JObject args, GH_Document? target)
        {
            if (toolCall.ToolSurface != AIToolSurface.Mcp
                || args["allowDisconnect"]?.ToObject<bool>() == true)
            {
                return;
            }

            var current = GhDocumentAccess.GetActiveDocument();
            if (current == null
                || ReferenceEquals(current, target)
                || !GhDocumentAccess.HostsEnabledMcpServer(current))
            {
                return;
            }

            var hostElsewhere = GhDocumentAccess.GetOpenDocuments()
                .Any(d => !ReferenceEquals(d, current) && GhDocumentAccess.HostsEnabledMcpServer(d));
            if (hostElsewhere)
            {
                return;
            }

            throw new InvalidOperationException(
                "Refusing to change the active document: the current document hosts the enabled SmartHopper MCP Server component that serves this session. Activating another document unloads it, stops the server mid-request, and drops this connection before the result can reach you. Ask the user to switch documents in the Grasshopper editor instead — the server restarts when the host document is shown again — or pass allowDisconnect: true to proceed anyway.");
        }
    }
}
