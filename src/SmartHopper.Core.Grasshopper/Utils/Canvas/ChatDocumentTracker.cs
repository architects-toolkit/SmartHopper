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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using GhJSON.Core;
using GhJSON.Core.SchemaModels;
using GhJSON.Core.Serialization;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.Serialization;
using Grasshopper.Kernel;
using Rhino;
using SmartHopper.Core.UI.Chat;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Grasshopper implementation of <see cref="IChatDocumentTracker"/>. Keeps one compact GhJSON baseline of the
    /// active document on disk (via <see cref="ChatCanvasBaselineStore"/>) and diffs it with
    /// <see cref="CanvasChangeSummary"/>. Serialization excludes runtime messages, runtime data, selection
    /// and metadata so only structural changes are reported.
    /// </summary>
    public sealed class ChatDocumentTracker : IChatDocumentTracker
    {
        private static readonly TimeSpan UiThreadTimeout = TimeSpan.FromSeconds(10);

        private readonly object syncRoot = new object();
        private readonly ChatCanvasBaselineStore store;
        private GhJsonStableIdMap idMap = new GhJsonStableIdMap();
        private Guid? documentId;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatDocumentTracker"/> class.
        /// </summary>
        /// <param name="store">Optional baseline store; a new one under the default folder is created when null.</param>
        public ChatDocumentTracker(ChatCanvasBaselineStore? store = null)
        {
            this.store = store ?? new ChatCanvasBaselineStore();
        }

        /// <inheritdoc />
        public IReadOnlyList<AIInteractionDocumentEvent> Start()
        {
            lock (this.syncRoot)
            {
                try
                {
                    var snapshot = Capture();
                    if (snapshot == null)
                    {
                        this.documentId = null;
                        return Array.Empty<AIInteractionDocumentEvent>();
                    }

                    return new[] { this.Track(snapshot, AIDocumentEventKind.ActiveDocument) };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatDocumentTracker] Start failed: {ex.Message}");
                    return Array.Empty<AIInteractionDocumentEvent>();
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<AIInteractionDocumentEvent> CollectChanges()
        {
            lock (this.syncRoot)
            {
                try
                {
                    var snapshot = Capture();
                    if (snapshot == null)
                    {
                        if (this.documentId == null)
                        {
                            return Array.Empty<AIInteractionDocumentEvent>();
                        }

                        this.documentId = null;
                        this.store.Delete();
                        return new[]
                        {
                            new AIInteractionDocumentEvent
                            {
                                Kind = AIDocumentEventKind.SwitchedDocument,
                                Content = "No active document",
                            },
                        };
                    }

                    if (this.documentId == null)
                    {
                        return new[] { this.Track(snapshot, AIDocumentEventKind.ActiveDocument) };
                    }

                    if (snapshot.DocumentId != this.documentId.Value)
                    {
                        // New document: rebaseline without diffing (the file context already reports its counts).
                        return new[] { this.Track(snapshot, AIDocumentEventKind.SwitchedDocument) };
                    }

                    var baselineJson = this.store.Load();
                    this.idMap.Apply(snapshot.Document);
                    this.SaveBaseline(snapshot.Document);
                    if (string.IsNullOrWhiteSpace(baselineJson))
                    {
                        return Array.Empty<AIInteractionDocumentEvent>();
                    }

                    var summary = CanvasChangeSummary.Build(GhJson.FromJson(baselineJson), snapshot.Document);
                    if (summary == null)
                    {
                        return Array.Empty<AIInteractionDocumentEvent>();
                    }

                    return new[]
                    {
                        new AIInteractionDocumentEvent
                        {
                            Kind = AIDocumentEventKind.CanvasChanged,
                            DocumentId = snapshot.DocumentId.ToString("D"),
                            DocumentName = snapshot.DocumentName,
                            Content = summary,
                        },
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatDocumentTracker] CollectChanges failed: {ex.Message}");
                    return Array.Empty<AIInteractionDocumentEvent>();
                }
            }
        }

        /// <inheritdoc />
        public void Rebaseline()
        {
            lock (this.syncRoot)
            {
                try
                {
                    var snapshot = Capture();

                    // A different (or no) document is reported as a switch by the next CollectChanges call.
                    if (snapshot == null || this.documentId == null || snapshot.DocumentId != this.documentId.Value)
                    {
                        return;
                    }

                    this.idMap.Apply(snapshot.Document);
                    this.SaveBaseline(snapshot.Document);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatDocumentTracker] Rebaseline failed: {ex.Message}");
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (this.syncRoot)
            {
                if (this.disposed)
                {
                    return;
                }

                this.disposed = true;
                this.store.Delete();
            }
        }

        private AIInteractionDocumentEvent Track(Snapshot snapshot, AIDocumentEventKind kind)
        {
            this.documentId = snapshot.DocumentId;
            this.idMap = new GhJsonStableIdMap();
            this.idMap.Apply(snapshot.Document);
            this.SaveBaseline(snapshot.Document);

            var content = kind == AIDocumentEventKind.ActiveDocument
                ? $"Active document: **{snapshot.DocumentName}**"
                : $"Switched to **{snapshot.DocumentName}**";

            return new AIInteractionDocumentEvent
            {
                Kind = kind,
                DocumentId = snapshot.DocumentId.ToString("D"),
                DocumentName = snapshot.DocumentName,
                Content = content,
            };
        }

        private void SaveBaseline(GhJsonDocument document)
        {
            if (!this.disposed)
            {
                this.store.Save(GhJson.ToJson(document, new WriteOptions { Indented = false }));
            }
        }

        /// <summary>
        /// Serializes the active document on the UI thread.
        /// </summary>
        private static Snapshot? Capture()
        {
            Snapshot? result = null;
            Exception? error = null;

            void Run()
            {
                try
                {
                    var doc = global::Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        return;
                    }

                    var options = new SerializationOptions
                    {
                        IncludeConnections = true,
                        IncludeGroups = true,
                        IncludeInternalizedData = true,
                        IncludeRuntimeData = false,
                        IncludeRuntimeMessages = false,
                        IncludeSelectedState = false,
                        IncludeMetadata = false,
                        AssignSequentialIds = true,
                    };

                    var objects = doc.Objects?.OfType<IGH_DocumentObject>().ToList() ?? new List<IGH_DocumentObject>();
                    var document = GhJsonGrasshopper.Serialize(objects, options);
                    result = new Snapshot(doc.DocumentID, GetDocumentName(doc), document);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }

            if (!RhinoApp.InvokeRequired)
            {
                Run();
            }
            else
            {
                // Not disposed on purpose: if the wait times out, the queued callback may still signal it later.
                var done = new ManualResetEventSlim(false);
                RhinoApp.InvokeOnUiThread((Action)(() =>
                {
                    try
                    {
                        Run();
                    }
                    finally
                    {
                        done.Set();
                    }
                }));

                if (!done.Wait(UiThreadTimeout))
                {
                    Debug.WriteLine("[ChatDocumentTracker] Timed out waiting for the UI thread");
                    return null;
                }
            }

            if (error != null)
            {
                Debug.WriteLine($"[ChatDocumentTracker] Capture failed: {error.Message}");
            }

            return result;
        }

        private static string GetDocumentName(GH_Document doc)
        {
            var path = doc.FilePath;
            return string.IsNullOrWhiteSpace(path) ? "Untitled" : Path.GetFileName(path);
        }

        private sealed class Snapshot
        {
            public Snapshot(Guid documentId, string documentName, GhJsonDocument document)
            {
                this.DocumentId = documentId;
                this.DocumentName = documentName;
                this.Document = document;
            }

            public Guid DocumentId { get; }

            public string DocumentName { get; }

            public GhJsonDocument Document { get; }
        }
    }
}
