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
using Eto.Forms;
using SmartHopper.Infrastructure.Mcp;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// WebChatUtils (helpers). Partial class containing shared helpers and configuration.
    /// </summary>
    public static partial class WebChatUtils
    {
        private const string DefaultWebChatContextFilter = "time, environment, file, selection, viewport";

        /// <summary>
        /// Ensures the Eto.Forms Application is initialized and attached.
        /// </summary>
        private static void EnsureEtoApplication()
        {
            if (Application.Instance == null)
            {
                DebugLog("[WebChatUtils] Initializing Eto.Forms application");
                var platform = Eto.Platform.Detect;
                new Application(platform).Attach();
            }
        }

        /// <summary>
        /// Brings a dialog to front, ensuring it is visible and focused.
        /// </summary>
        private static void BringToFrontAndFocus(WebChatDialog dialog)
        {
            dialog.EnsureVisibility();
            dialog.BringToFront();
            dialog.Focus();
        }

        /// <summary>
        /// Shared helper to open or reuse a WebChatDialog on the UI thread, attach handlers, and bring to front.
        /// Optionally wires closed cleanup to a TaskCompletionSource for ShowWebChatDialog flows.
        /// </summary>
        private static WebChatDialog OpenOrReuseDialogInternal(
            AIRequestCall request,
            Guid componentId,
            Action<string>? progressReporter,
            Action<AIReturn>? onUpdate,
            TaskCompletionSource<AIReturn>? completionTcs,
            bool pushCurrentImmediately,
            bool generateGreeting,
            out bool reused)
        {
            EnsureEtoApplication();

            if (componentId != default && OpenDialogs.TryGetValue(componentId, out WebChatDialog existingDialog))
            {
                DebugLog("[WebChatUtils] Reusing existing dialog for component");
                BringToFrontAndFocus(existingDialog);
                AttachOrReplaceUpdateHandler(componentId, existingDialog, progressReporter, onUpdate, pushCurrentImmediately: pushCurrentImmediately);

                // For ShowWebChatDialog flows, immediate completion mirrors previous behavior
                if (completionTcs != null)
                {
                    try { completionTcs.TrySetResult(existingDialog.GetLastReturn()); } catch { /* ignore */ }
                }

                reused = true;
                return existingDialog;
            }

            DebugLog("[WebChatUtils] Creating web chat dialog");
            var dialog = new WebChatDialog(request, progressReporter, generateGreeting: generateGreeting);
            if (componentId != default)
            {
                OpenDialogs[componentId] = dialog;
            }

            // Closed cleanup + optional result propagation
            WireClosedCleanup(componentId, dialog, progressReporter, completionTcs);

            // Incremental updates
            AttachOrReplaceUpdateHandler(componentId, dialog, progressReporter, onUpdate);

            dialog.Title = $"SmartHopper AI Chat - {request.Model} ({request.Provider})";
            dialog.Show();
            BringToFrontAndFocus(dialog);

            reused = false;
            return dialog;
        }

        /// <summary>
        /// Attaches (or replaces) a ChatUpdated handler for a component/dialog pair and tracks it.
        /// </summary>
        private static void AttachOrReplaceUpdateHandler(
            Guid componentId,
            WebChatDialog dialog,
            Action<string>? progressReporter,
            Action<AIReturn>? onUpdate,
            bool pushCurrentImmediately = false)
        {
            if (onUpdate == null)
            {
                return;
            }

            if (UpdateHandlers.TryGetValue(componentId, out var oldHandler))
            {
                try
                {
                    dialog.ChatUpdated -= oldHandler;
                }
                catch
                {
                    /* ignore */
                }
            }

            EventHandler<AIReturn> handler = (s, snapshot) =>
            {
                try
                {
                    progressReporter?.Invoke("Chatting...");
                    onUpdate?.Invoke(snapshot);
                }
                catch (Exception updEx)
                {
                    DebugLog($"[WebChatUtils] ChatUpdated handler error: {updEx.Message}");
                }
            };

            dialog.ChatUpdated += handler;
            UpdateHandlers[componentId] = handler;

            if (pushCurrentImmediately)
            {
                try
                {
                    var current = dialog.GetLastReturn();
                    if (current != null)
                    {
                        onUpdate(current);
                    }
                }
                catch
                {
                    /* ignore */
                }
            }
        }

        /// <summary>
        /// Wires dialog closed cleanup and optionally completes a TaskCompletionSource with the last return.
        /// </summary>
        private static void WireClosedCleanup(Guid componentId, WebChatDialog dialog, Action<string>? progressReporter, TaskCompletionSource<AIReturn>? tcs = null)
        {
            dialog.Closed += (sender, e) =>
            {
                DebugLog("[WebChatUtils] Dialog closed");
                try
                {
                    if (componentId != default)
                    {
                        OpenDialogs.Remove(componentId);

                        if (UpdateHandlers.TryGetValue(componentId, out var handler))
                        {
                            try
                            {
                                dialog.ChatUpdated -= handler;
                            }
                            catch
                            {
                                /* ignore */
                            }

                            UpdateHandlers.Remove(componentId);
                        }
                    }

                    // Ensure the hosting component isn't left in a stale "Chatting..." status.
                    // The Grasshopper component's message is driven by the progressReporter.
                    progressReporter?.Invoke("Ready");

                    var last = dialog.GetLastReturn();
                    tcs?.TrySetResult(last);
                }
                catch (Exception ex)
                {
                    DebugLog($"[WebChatUtils] Closed cleanup error: {ex.Message}");
                }
            };
        }

        private const string ToolDiscoveryNote =
            "\n\n## Chat Workflow\n" +
            "For a straightforward explanation or greeting, answer directly; do not call tools just to demonstrate activity. " +
            "For canvas work, inspect the smallest relevant scope first. When the user says 'this' or 'these', " +
            "use gh_get with attrFilters: ['+selected']; use detail: 'summary' and request only needed fields. " +
            "If nothing is selected, ask which objects they mean rather than choosing the whole document. " +
            "Inspect runtime messages, connections and values when diagnosing; do not infer correctness from appearance alone.\n" +
            "Use tools actually offered in this request or schemas returned by discovery; availability depends on scope. " +
            "Before the first action in a task that needs an unavailable capability, call search_tools with a short, " +
            "specific action query (for example, 'connect components' or 'canvas screenshot') when search_tools is " +
            "offered; otherwise explain the unavailable capability. " +
            "Reuse discovered schemas while they remain in context; do not search again every turn. " +
            "Discovery returns definitions, not completed work: invoke the chosen tool with its required arguments. " +
            "If no result fits, try one more precise query, then explain the limitation; never invent names or arguments.\n" +
            "When available, smarthopper_readme supplies focused local guidance (for example 'data-trees', 'debugging', " +
            "'geometry' or 'research'); smarthopper_workflows supplies a named workflow for unfamiliar multi-step work. " +
            "Read the relevant guide when uncertain, not every guide on every turn. search_tools finds capabilities, " +
            "not factual answers. For version-specific or uncertain technical claims, research rather than guess: " +
            "prefer official documentation, discover a documentation reader when needed, or use mcneel_forum_search " +
            "and mcneel_forum_post_get for community evidence. Read relevant posts before relying on excerpts; " +
            "post_get needs the returned global post id, not a topic id or topic-local post number. " +
            "Cite source URLs and note version or plug-in assumptions. Do not send private canvas data to external search " +
            "unless the user has authorized it; use generic technical queries.\n" +
            "Use ask_user when a consequential decision or missing input blocks progress; otherwise state a reasonable " +
            "assumption and proceed. Use canvas_point to explain a specific location, not unsolicited repeated viewport moves. " +
            "Prefer a small native-component solution. Present generated GhJSON through show_ghjson, never raw JSON in chat; " +
            "use generation-and-placement only when placement was requested. Respect each mutation review and verify " +
            "the affected errors or outputs after accepted changes. Finish with the result, what was verified, " +
            "and any remaining limitation; do not expose internal ids or a long tool transcript.";

        private const string ConductorNote =
            "\n\n## Background Work\n" +
            "Handle simple tasks yourself. Delegate only independent, substantial work when useful, after discovering " +
            "the background-agent tools if needed. A successful spawn returns a worker id; only then describe a worker " +
            "as started. Use returned status/report evidence, not earlier assistant claims, to describe progress. " +
            "Workers report back automatically and the conversation resumes even after your turn ends, so do not " +
            "repeatedly poll. Wait only if the current answer depends on the result. If a confirmed worker is still " +
            "running when you finish, name it and explain that the chat will resume when its report arrives.";

        internal static AIRequestCall CreateWebChatRequest(
            string providerName,
            string modelName,
            string origin,
            string systemPrompt,
            string toolFilter)
        {
            var requiredCapabilities = ComputeRequiredCapabilities(toolFilter);
            var composedSystemPrompt = AgentKnowledgeCatalog.ComposeGrasshopperSystemPrompt(systemPrompt);
            if (!string.Equals(toolFilter, "-*", StringComparison.Ordinal))
            {
                composedSystemPrompt += " " + ToolDiscoveryNote + " " + ConductorNote;
            }

            var request = new AIRequestCall
            {
                ToolSurface = SmartHopper.ProviderSdk.Hosting.AIToolSurface.Chat,
            };
            request.Initialize(
                provider: providerName,
                model: modelName,
                systemPrompt: composedSystemPrompt,
                origin: origin,
                capability: requiredCapabilities,
                toolFilter: toolFilter);

            request.Body = ApplyDefaultWebChatContext(request.Body);
            return request;
        }

        private static AIBody ApplyDefaultWebChatContext(AIBody body)
        {
            return AIBodyBuilder.FromImmutable(body)
                .WithContextFilter(DefaultWebChatContextFilter)
                .Build();
        }

        private static AICapability ComputeRequiredCapabilities(string toolFilter)
        {
            return toolFilter != "-*" ? AICapability.ToolChat : AICapability.Text2Text;
        }
    }
}
