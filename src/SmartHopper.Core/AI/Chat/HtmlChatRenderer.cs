/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Utility class for rendering chat messages as HTML for WebView display.
    /// </summary>
    public class HtmlChatRenderer
    {
        private readonly MarkdownPipeline _markdownPipeline;
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
        /// <param name="role">The role of the message sender (user, assistant, system).</param>
        /// <param name="response">The AIResponse containing metrics data.</param>
        /// <returns>HTML representation of the message.</returns>
        public string GenerateMessageHtml(string role, AIResponse response)
        {
            Debug.WriteLine($"[HtmlChatRenderer] Generating message HTML for role: {role}");

            try
            {
                string displayRole;

                // Determine display role based on message role
                switch (role)
                {
                    case "user":
                        displayRole = "You";
                        break;
                    case "assistant":
                        displayRole = "AI";
                        break;
                    case "system":
                        displayRole = "System";
                        break;
                    case "tool":
                        displayRole = "Tool";
                        break;
                    case "tool_call":
                        displayRole = "Calling a tool...";
                        break;
                    default:
                        displayRole = role;
                        break;
                }

                try
                {
                    // Create timestamp for the message
                    string timestamp = DateTime.Now.ToString("HH:mm");
                    // Use the resource manager to create the message HTML
                    string messageHtml = _resourceManager.CreateMessageHtml(
                        role,
                        WebUtility.HtmlEncode(displayRole),
                        timestamp,
                        response);
                    Debug.WriteLine($"[HtmlChatRenderer] Message HTML created, length: {messageHtml?.Length ?? 0}");

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
