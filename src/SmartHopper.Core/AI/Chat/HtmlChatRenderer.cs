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
using System.Text;
using System.Web;
using Markdig;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Utility class for rendering chat messages as HTML for WebView display.
    /// </summary>
    public class HtmlChatRenderer
    {
        private readonly MarkdownPipeline _markdownPipeline;

        /// <summary>
        /// Initializes a new instance of the HtmlChatRenderer class.
        /// </summary>
        public HtmlChatRenderer()
        {
            // Configure Markdig pipeline with needed extensions
            _markdownPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .Build();
        }

        /// <summary>
        /// Gets the initial HTML structure for the chat interface.
        /// </summary>
        /// <returns>The initial HTML content.</returns>
        public string GetInitialHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            margin: 0;
            padding: 10px;
            background-color: #f5f5f5;
            line-height: 1.5;
        }
        #chat-container {
            display: flex;
            flex-direction: column;
            max-width: 100%;
            margin: 0 auto;
        }
        .message {
            display: flex;
            margin-bottom: 10px;
            max-width: 80%;
        }
        .message.user {
            align-self: flex-end;
        }
        .message.assistant, .message.system {
            align-self: flex-start;
        }
        .message-content {
            border-radius: 10px;
            padding: 8px 12px;
            box-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
        }
        .user .message-content {
            background-color: #dcf8c6;
            color: #000;
        }
        .assistant .message-content {
            background-color: #fff;
            color: #000;
        }
        .system .message-content {
            background-color: #f0f0f0;
            color: #666;
            font-style: italic;
        }
        .message-sender {
            font-size: 0.8em;
            color: #666;
            margin-bottom: 2px;
        }
        pre {
            background-color: #f8f8f8;
            border: 1px solid #ddd;
            border-radius: 4px;
            padding: 8px;
            overflow-x: auto;
        }
        code {
            font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
            font-size: 0.9em;
            background-color: #f0f0f0;
            padding: 2px 4px;
            border-radius: 3px;
        }
        pre code {
            background-color: transparent;
            padding: 0;
        }
        a {
            color: #0366d6;
            text-decoration: none;
        }
        a:hover {
            text-decoration: underline;
        }
        blockquote {
            border-left: 4px solid #ddd;
            margin-left: 0;
            padding-left: 10px;
            color: #666;
        }
        table {
            border-collapse: collapse;
            width: 100%;
            margin: 10px 0;
        }
        th, td {
            border: 1px solid #ddd;
            padding: 8px;
            text-align: left;
        }
        th {
            background-color: #f2f2f2;
        }
        img {
            max-width: 100%;
            height: auto;
        }
    </style>
</head>
<body>
    <div id='chat-container'></div>
    <script>
        function addMessage(messageHtml) {
            const chatContainer = document.getElementById('chat-container');
            chatContainer.innerHTML += messageHtml;
        }
        
        function scrollToBottom() {
            window.scrollTo(0, document.body.scrollHeight);
        }
    </script>
</body>
</html>";
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
            
            // Build message HTML
            var messageHtml = new StringBuilder();
            messageHtml.AppendLine($"<div class='message {role}'>");
            messageHtml.AppendLine($"  <div>");
            messageHtml.AppendLine($"    <div class='message-sender'>{HttpUtility.HtmlEncode(displayRole)}</div>");
            messageHtml.AppendLine($"    <div class='message-content'>{htmlContent}</div>");
            messageHtml.AppendLine($"  </div>");
            messageHtml.AppendLine($"</div>");
            
            return messageHtml.ToString();
        }
    }
}
