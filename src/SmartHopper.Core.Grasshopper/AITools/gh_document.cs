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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for working with the Grasshopper documents open in Rhino: list them, switch the active
    /// document, create a blank document, and open a document from disk. Reading a non-active document is done
    /// with <c>gh_get</c> (<c>documentId</c>) and saving with <c>gh_document_save</c> (<c>documentId</c>).
    /// </summary>
    public class gh_document : IAIToolProvider
    {
        private const string DocumentOutputSchema = @"{ ""type"": ""object"", ""properties"": { ""documentId"": { ""type"": ""string"" }, ""name"": { ""type"": ""string"" }, ""filePath"": { ""type"": [""string"", ""null""] }, ""isActive"": { ""type"": ""boolean"" }, ""isModified"": { ""type"": ""boolean"" }, ""objectCount"": { ""type"": ""integer"" } } }";

        private const AIToolSurface InteractiveSurfaces = AIToolSurface.Chat | AIToolSurface.Direct | AIToolSurface.Mcp;

        /// <summary>
        /// Returns the document tools.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_document_list",
                description: "List the Grasshopper documents open in Rhino with their documentId, name, filePath, isActive, isModified and objectCount. Use the documentId with gh_get, gh_document_switch or gh_document_save to work with a document that is not active.",
                category: "Document",
                parametersSchema: @"{ ""type"": ""object"", ""properties"": {} }",
                execute: ListAsync,
                mutatesCanvas: false,
                tags: new[] { "document", "list" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""activeDocumentId"": { ""type"": [""string"", ""null""] }, ""documents"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));

            yield return new AITool(
                name: "gh_document_switch",
                description: "Show another open Grasshopper document in the canvas, making it the active document for all canvas tools. Get documentId values from gh_document_list.",
                category: "ViewControl",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""documentId"": { ""type"": ""string"", ""description"": ""documentId of an open document (from gh_document_list)."" }
                    },
                    ""required"": [""documentId""]
                }",
                execute: SwitchAsync,
                mutatesCanvas: false,
                tags: new[] { "document", "switch", "view" },
                outputSchema: DocumentOutputSchema,
                annotations: new AIToolAnnotations(readOnlyHint: false),
                surfaces: InteractiveSurfaces);

            yield return new AITool(
                name: "gh_document_new",
                description: "Create a new blank Grasshopper document and make it the active document. Use gh_document_save with a filePath to save it to disk.",
                category: "Document",
                parametersSchema: @"{ ""type"": ""object"", ""properties"": {} }",
                execute: NewAsync,
                mutatesCanvas: false,
                tags: new[] { "document", "new", "file" },
                outputSchema: DocumentOutputSchema,
                annotations: new AIToolAnnotations(readOnlyHint: false),
                surfaces: InteractiveSurfaces);

            yield return new AITool(
                name: "gh_document_open",
                description: "Open a Grasshopper document (.gh or .ghx) from an absolute file path and make it the active document. If the file is already open, it is activated instead of opened twice.",
                category: "Document",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filePath"": { ""type"": ""string"", ""description"": ""Absolute path to an existing .gh or .ghx file."" }
                    },
                    ""required"": [""filePath""]
                }",
                execute: OpenAsync,
                mutatesCanvas: false,
                tags: new[] { "document", "open", "file" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""alreadyOpen"": { ""type"": ""boolean"" }, ""document"": { ""type"": ""object"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: false),
                surfaces: InteractiveSurfaces);
        }

        private static Task<AIReturn> ListAsync(AIToolCall toolCall)
        {
            return Run(toolCall, _ =>
            {
                var active = GhDocumentAccess.GetActiveDocument();
                var documents = new JArray(GhDocumentAccess.GetOpenDocuments().Select(d => GhDocumentAccess.Describe(d, active)));
                return new JObject
                {
                    ["activeDocumentId"] = active?.DocumentID.ToString("D"),
                    ["documents"] = documents,
                };
            });
        }

        private static Task<AIReturn> SwitchAsync(AIToolCall toolCall)
        {
            return Run(toolCall, args =>
            {
                var documentId = args["documentId"]?.ToString();
                var doc = GhDocumentAccess.FindById(documentId)
                    ?? throw new InvalidOperationException($"No open document with documentId '{documentId}'. Call gh_document_list to get valid ids.");

                if (!GhDocumentAccess.Activate(doc))
                {
                    throw new InvalidOperationException("Could not activate the document: no Grasshopper canvas is available.");
                }

                return GhDocumentAccess.Describe(doc, doc);
            });
        }

        private static Task<AIReturn> NewAsync(AIToolCall toolCall)
        {
            return Run(toolCall, _ =>
            {
                var doc = GhDocumentAccess.CreateBlank()
                    ?? throw new InvalidOperationException("Could not create a document: no Grasshopper canvas is available.");
                return GhDocumentAccess.Describe(doc, doc);
            });
        }

        private static Task<AIReturn> OpenAsync(AIToolCall toolCall)
        {
            return Run(toolCall, args =>
            {
                if (!GrasshopperFilePath.TryNormalize(args["filePath"]?.ToString(), mustExist: true, out var fullPath, out var error))
                {
                    throw new InvalidOperationException(error);
                }

                var doc = GhDocumentAccess.Open(fullPath, out var alreadyOpen)
                    ?? throw new InvalidOperationException($"Could not open '{fullPath}'. The file may be invalid or no Grasshopper canvas is available.");

                return new JObject
                {
                    ["alreadyOpen"] = alreadyOpen,
                    ["document"] = GhDocumentAccess.Describe(doc, doc),
                };
            });
        }

        private static Task<AIReturn> Run(AIToolCall toolCall, Func<JObject, JObject> action)
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
    }
}
