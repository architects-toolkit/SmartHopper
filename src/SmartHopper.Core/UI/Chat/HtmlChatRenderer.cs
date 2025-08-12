/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * HTML Chat Renderer for WebView-based chat interface.
 * This class provides methods for converting chat messages to HTML format.
 */

using System;
using System.Diagnostics;
using System.Net;
using Markdig;
using SmartHopper.Infrastructure.AICall;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Utility class for rendering chat messages as HTML for WebView display.
    /// </summary>
    internal class HtmlChatRenderer
    {
        private readonly ChatResourceManager _resourceManager;

        /// <summary>
        /// Initializes a new instance of the HtmlChatRenderer class.
        /// </summary>
        public HtmlChatRenderer()
        {
            Debug.WriteLine("[HtmlChatRenderer] Initializing HtmlChatRenderer");

            // Configure Markdig pipeline with needed extensions
            _markdownPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Default)
                .UseGridTables()
                .UsePipeTables()
                .UseListExtras()
                .UseTaskLists()
                .UseAutoLinks()
                .UseGenericAttributes()
                .Build();

            // Initialize the resource manager
            _resourceManager = new ChatResourceManager();
            Debug.WriteLine("[HtmlChatRenderer] Resource manager initialized");
        }

        /// <summary>
        /// Gets the initial HTML structure for the chat interface.
        /// </summary>
        /// <returns>The initial HTML content.</returns>
        public string GetInitialHtml()
        {
            Debug.WriteLine("[HtmlChatRenderer] Getting initial HTML");

            try
            {
                string html = _resourceManager.GetCompleteHtml();
                Debug.WriteLine($"[HtmlChatRenderer] Complete HTML retrieved, length: {html?.Length ?? 0}");

                // For debugging, output the first 200 characters of the HTML
                if (html != null && html.Length > 0)
                {
                    string preview = html.Length > 200 ? html.Substring(0, 200) + "..." : html;
                    Debug.WriteLine($"[HtmlChatRenderer] HTML preview: {preview}");
                }

                return html;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HtmlChatRenderer] Error getting complete HTML: {ex.Message}");
                Debug.WriteLine($"[HtmlChatRenderer] Stack trace: {ex.StackTrace}");

                // Use the error template from resources
                return _resourceManager.GetErrorTemplate(ex.Message);
            }
        }

        /// <summary>
        /// Generates HTML for a chat message.
        /// </summary>
        /// <param name="interaction">The AIInteraction containing metrics data.</param>
        /// <returns>HTML representation of the message.</returns>
        public string GenerateMessageHtml(IAIInteraction interaction)
        {
            Debug.WriteLine($"[HtmlChatRenderer] Generating message HTML for agent: {interaction.Agent.ToString()}");
            
            try
            {
                string displayRole = interaction.Agent.ToDescription();

                try
                {
                    // Create timestamp for the message
                    string timestamp = DateTime.Now.ToString("HH:mm");
                    // Use the resource manager to create the message HTML
                    string messageHtml = _resourceManager.CreateMessageHtml(
                        interaction.Agent,
                        displayRole,
                        timestamp,
                        interaction);
                    Debug.WriteLine($"[HtmlChatRenderer] Message HTML created, length: {messageHtml?.Length ?? 0}");

                    Debug.WriteLine($"[HtmlChatRenderer] Message HTML (truncated): {messageHtml?.Substring(0, Math.Min(100, messageHtml.Length))}...");

                    return messageHtml;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HtmlChatRenderer] Error creating message HTML with resource manager: {ex.Message}");

                    // Create a simple message HTML as fallback
                    return "error";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HtmlChatRenderer] Error generating message HTML: {ex.Message}");
                Debug.WriteLine($"[HtmlChatRenderer] Stack trace: {ex.StackTrace}");

                // Create a simple message HTML as fallback
                return "error";
            }
        }
    }
}
