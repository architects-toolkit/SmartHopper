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
using System.Threading.Tasks;
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
        public const string DocumentOutputSchema = @"{ ""type"": ""object"", ""properties"": { ""documentId"": { ""type"": ""string"" }, ""name"": { ""type"": ""string"" }, ""filePath"": { ""type"": [""string"", ""null""] }, ""isActive"": { ""type"": ""boolean"" }, ""isModified"": { ""type"": ""boolean"" }, ""objectCount"": { ""type"": ""integer"" } } }";

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
        /// Finds an open document by <paramref name="documentId"/> or throws a model-facing error.
        /// </summary>
        /// <param name="documentId">The document id.</param>
        /// <returns>The document.</returns>
        public static Grasshopper.Kernel.GH_Document RequireDocument(string? documentId)
        {
            return GhDocumentAccess.FindById(documentId)
                ?? throw new InvalidOperationException($"No open document with documentId '{documentId}'. Call gh_document_list to get valid ids.");
        }
    }
}
