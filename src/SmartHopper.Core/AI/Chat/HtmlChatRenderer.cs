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

using System.Net;
using System.Text;
using Markdig;

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
        }

        /// <summary>
        /// Gets the initial HTML structure for the chat interface.
        /// </summary>
        /// <returns>The initial HTML content.</returns>
        public string GetInitialHtml()
        {
            return _resourceManager.GetCompleteHtml();
        }

        /// <summary>
        /// Generates HTML for a chat message.
        /// </summary>
        /// <param name="role">The role of the message sender (user, assistant, system).</param>
        /// <param name="content">The message content.</param>
        /// <returns>HTML representation of the message.</returns>
        public string GenerateMessageHtml(string role, string content)
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
                default:
                    displayRole = role;
                    break;
            }
            
            // Convert markdown to HTML
            string htmlContent = Markdown.ToHtml(content, _markdownPipeline);
            
            // Use the resource manager to create the message HTML
            return _resourceManager.CreateMessageHtml(role, WebUtility.HtmlEncode(displayRole), htmlContent);
        }
    }
}
