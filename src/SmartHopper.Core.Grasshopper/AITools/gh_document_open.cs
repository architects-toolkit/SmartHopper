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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for opening a Grasshopper document from a file path.
    /// </summary>
    public class gh_document_open : IAIToolProvider
    {
        /// <summary>
        /// Returns the gh_document_open tool.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_document_open",
                description: "Open a Grasshopper document (.gh or .ghx) from an absolute file path and make it the active document. If the file is already open, it is activated instead of opened twice.",
                category: "Canvas.Documents",
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
                annotations: new AIToolAnnotations(readOnlyHint: false, openWorldHint: true),
                surfaces: GhDocumentTools.InteractiveSurfaces);
        }

        private static Task<AIReturn> OpenAsync(AIToolCall toolCall)
        {
            return GhDocumentTools.Run(toolCall, args =>
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
    }
}
