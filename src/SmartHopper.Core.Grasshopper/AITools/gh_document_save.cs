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
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for saving the current Grasshopper document.
    /// </summary>
    public class gh_document_save : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_document_save";

        /// <summary>
        /// Returns AI tools for saving the current Grasshopper document.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Save a Grasshopper document. Defaults to the active document; pass documentId (from gh_document_list) to save another open document. If no filePath is provided, the document is saved to its existing location. Provide an absolute .gh or .ghx file path to save a copy or an unsaved document. The user reviews and approves the target path before anything is written.",
                category: "Canvas.Documents",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filePath"": {
                            ""type"": [""string"", ""null""],
                            ""description"": ""Optional absolute .gh or .ghx file path to save the document. If omitted, the current document path is used.""
                        },
                        ""documentId"": {
                            ""type"": [""string"", ""null""],
                            ""description"": ""Optional documentId of an open document (from gh_document_list). Defaults to the active document.""
                        }
                    }
                }",
                execute: this.GhDocumentSaveToolAsync,
                mutatesCanvas: true,
                tags: new[] { "document", "save", "file" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""filePath"": { ""type"": [""string"", ""null""] }, ""saved"": { ""type"": ""boolean"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: false, destructiveHint: true, openWorldHint: true),
                surfaces: GhDocumentTools.InteractiveSurfaces,
                activityLabel: "saving the document",
                resultLabel: "saved the document");
        }

        private Task<AIReturn> GhDocumentSaveToolAsync(AIToolCall toolCall)
        {
            return GhDocumentTools.RunAsync(toolCall, async args =>
            {
                var documentId = args["documentId"]?.ToString();
                var doc = string.IsNullOrWhiteSpace(documentId)
                    ? CanvasAccess.GetCurrentCanvas()
                    : GhDocumentTools.RequireDocument(documentId);
                if (doc == null)
                {
                    throw new InvalidOperationException("No active document.");
                }

                var filePath = args["filePath"]?.ToString();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    filePath = null;
                }
                else if (!GrasshopperFilePath.TryNormalize(filePath, mustExist: false, out var fullPath, out var pathError))
                {
                    throw new InvalidOperationException(pathError);
                }
                else
                {
                    filePath = fullPath;
                }

                var targetPath = filePath ?? doc.FilePath;
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    throw new InvalidOperationException("Could not save the document. Unsaved documents need a filePath.");
                }

                // Writing to a model-supplied path can overwrite any .gh/.ghx file the
                // user can write, so the save goes through the same consent review as
                // every other mutating tool. The review item names the target path and
                // flags whether an existing file will be overwritten.
                var review = CanvasChangeReviewService.CreateDocumentSaveSession(
                    this.toolName,
                    GhDocumentAccess.GetDisplayName(doc),
                    targetPath,
                    willOverwrite: File.Exists(targetPath));
                var approved = await CanvasChangeReviewService.ReviewAsync(
                    review,
                    toolCall.InvocationContext,
                    toolCall.CancellationToken).ConfigureAwait(false);
                if (!approved || review.AcceptedCount == 0)
                {
                    return new JObject
                    {
                        ["saved"] = false,
                        ["filePath"] = null,
                        ["documentId"] = doc.DocumentID.ToString("D"),
                        ["message"] = "The user rejected the save.",
                    };
                }

                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var savedPath = CanvasAccess.SaveDocument(doc, targetPath);
                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    throw new InvalidOperationException("Could not save the document.");
                }

                return new JObject
                {
                    ["saved"] = true,
                    ["filePath"] = savedPath,
                    ["documentId"] = doc.DocumentID.ToString("D"),
                };
            });
        }
    }
}
