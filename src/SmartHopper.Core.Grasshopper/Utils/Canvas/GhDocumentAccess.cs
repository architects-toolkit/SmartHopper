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
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Access to the Grasshopper documents open in the document server: list, find, activate, create and open.
    /// All members marshal to the Rhino UI thread.
    /// </summary>
    public static class GhDocumentAccess
    {
        /// <summary>
        /// Gets the documents currently open in Grasshopper.
        /// </summary>
        /// <returns>The open documents, in document-server order.</returns>
        public static IReadOnlyList<GH_Document> GetOpenDocuments()
        {
            return CanvasAccess.RunOnUiThread(() =>
            {
                var result = new List<GH_Document>();
                var server = Instances.DocumentServer;
                if (server != null)
                {
                    foreach (GH_Document doc in server)
                    {
                        if (doc != null)
                        {
                            result.Add(doc);
                        }
                    }
                }

                return (IReadOnlyList<GH_Document>)result;
            });
        }

        /// <summary>
        /// Gets the document shown in the active canvas.
        /// </summary>
        /// <returns>The active document, or null.</returns>
        public static GH_Document? GetActiveDocument()
        {
            return CanvasAccess.RunOnUiThread(() => Instances.ActiveCanvas?.Document);
        }

        /// <summary>
        /// Finds an open document by its <see cref="GH_Document.DocumentID"/>.
        /// </summary>
        /// <param name="documentId">The document id (GUID string).</param>
        /// <returns>The document, or null when not found or the id is invalid.</returns>
        public static GH_Document? FindById(string? documentId)
        {
            if (!Guid.TryParse(documentId, out var id))
            {
                return null;
            }

            return GetOpenDocuments().FirstOrDefault(d => d.DocumentID == id);
        }

        /// <summary>
        /// Finds an open document saved at <paramref name="fullPath"/>.
        /// </summary>
        /// <param name="fullPath">Normalized absolute file path.</param>
        /// <returns>The document, or null.</returns>
        public static GH_Document? FindByPath(string fullPath)
        {
            return GetOpenDocuments().FirstOrDefault(d =>
                !string.IsNullOrWhiteSpace(d.FilePath)
                && string.Equals(Path.GetFullPath(d.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the display name of a document (file name, or "Untitled").
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <returns>The display name.</returns>
        public static string GetDisplayName(GH_Document doc)
        {
            var path = doc?.FilePath;
            return string.IsNullOrWhiteSpace(path) ? "Untitled" : Path.GetFileName(path);
        }

        /// <summary>
        /// Describes a document for tool results.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <param name="activeDocument">The active document, used for the <c>isActive</c> flag.</param>
        /// <returns>The JSON description.</returns>
        public static JObject Describe(GH_Document doc, GH_Document? activeDocument)
        {
            return CanvasAccess.RunOnUiThread(() => new JObject
            {
                ["documentId"] = doc.DocumentID.ToString("D"),
                ["name"] = GetDisplayName(doc),
                ["filePath"] = string.IsNullOrWhiteSpace(doc.FilePath) ? null : doc.FilePath,
                ["isActive"] = ReferenceEquals(doc, activeDocument),
                ["isModified"] = doc.IsModified,
                ["objectCount"] = doc.ObjectCount,
            });
        }

        /// <summary>
        /// Shows <paramref name="doc"/> in the active canvas.
        /// </summary>
        /// <param name="doc">The document to activate.</param>
        /// <returns>True when the canvas now shows the document.</returns>
        public static bool Activate(GH_Document doc)
        {
            return CanvasAccess.RunOnUiThread(() =>
            {
                var canvas = Instances.ActiveCanvas;
                if (canvas == null || doc == null)
                {
                    return false;
                }

                if (!ReferenceEquals(canvas.Document, doc))
                {
                    canvas.Document = doc;
                }

                canvas.Refresh();
                return ReferenceEquals(canvas.Document, doc);
            });
        }

        /// <summary>
        /// Creates a blank document, registers it with the document server and activates it.
        /// </summary>
        /// <returns>The new document, or null when no canvas is available.</returns>
        public static GH_Document? CreateBlank()
        {
            return CanvasAccess.RunOnUiThread(() =>
            {
                if (Instances.ActiveCanvas == null || Instances.DocumentServer == null)
                {
                    return null;
                }

                var doc = new GH_Document();
                Instances.DocumentServer.AddDocument(doc);
                return Activate(doc) ? doc : null;
            });
        }

        /// <summary>
        /// Opens a document from disk and activates it. A document already open at that path is activated instead.
        /// </summary>
        /// <param name="fullPath">Normalized absolute path to an existing .gh/.ghx file.</param>
        /// <param name="alreadyOpen">True when the file was already open.</param>
        /// <returns>The opened document, or null when it could not be read.</returns>
        public static GH_Document? Open(string fullPath, out bool alreadyOpen)
        {
            var existing = FindByPath(fullPath);
            alreadyOpen = existing != null;
            if (existing != null)
            {
                return Activate(existing) ? existing : null;
            }

            return CanvasAccess.RunOnUiThread(() =>
            {
                if (Instances.ActiveCanvas == null || Instances.DocumentServer == null)
                {
                    return null;
                }

                var io = new GH_DocumentIO();
                if (!io.Open(fullPath) || io.Document == null)
                {
                    return null;
                }

                var doc = io.Document;
                Instances.DocumentServer.AddDocument(doc);
                return Activate(doc) ? doc : null;
            });
        }
    }
}
