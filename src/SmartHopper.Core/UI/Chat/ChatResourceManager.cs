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
 * Resource manager for the chat interface.
 * This class provides methods for loading and managing HTML, CSS, and JavaScript resources.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Markdig;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Manages resources for the chat interface, including HTML templates, CSS, and JavaScript.
    /// </summary>
    internal class ChatResourceManager
    {
        private string _cachedChatTemplate;
        private string _cachedMessageTemplate;
        private string _cachedErrorTemplate;
        private string _cachedCssContent;
        private string _cachedJsContent;
        private readonly MarkdownPipeline _markdownPipeline;

        // Resource names
        private const string CSS_RESOURCE = "SmartHopper.Core.UI.Chat.Resources.css.chat-styles.css";
        private const string JS_RESOURCE = "SmartHopper.Core.UI.Chat.Resources.js.chat-script.js";
        private const string CHAT_TEMPLATE_RESOURCE = "SmartHopper.Core.UI.Chat.Resources.templates.chat-template.html";
        private const string MESSAGE_TEMPLATE_RESOURCE = "SmartHopper.Core.UI.Chat.Resources.templates.message-template.html";
        private const string ERROR_TEMPLATE_RESOURCE = "SmartHopper.Core.UI.Chat.Resources.templates.error-template.html";

        /// <summary>
        /// Initializes a new instance of the ChatResourceManager class.
        /// </summary>
        public ChatResourceManager()
        {
            Debug.WriteLine("[ChatResourceManager] Initializing ChatResourceManager");

            // List all embedded resources for debugging
            ListAllEmbeddedResources();

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
        }

        /// <summary>
        /// Creates a complete HTML document with embedded CSS and JS for offline use.
        /// </summary>
        /// <returns>The complete HTML document.</returns>
        public string GetCompleteHtml()
        {
            Debug.WriteLine("[ChatResourceManager] Creating complete HTML with embedded resources");

            try
            {
                // Load all required resources
                string cssContent = GetCssContent();
                string jsContent = GetJsContent();
                string messageTemplate = GetMessageTemplate();
                string chatTemplate = GetChatTemplate();

                // Escape single quotes in the message template to avoid breaking the JavaScript
                messageTemplate = messageTemplate.Replace("'", "\\'");

                // Replace all placeholders with actual content
                string completeHtml = chatTemplate
                    .Replace("{{cssChat}}", cssContent)
                    .Replace("{{jsChat}}", jsContent)
                    .Replace("{{messageTemplate}}", messageTemplate);

                Debug.WriteLine($"[ChatResourceManager] Complete HTML created, length: {completeHtml?.Length ?? 0}");

                // Write the complete HTML to a debug file for inspection
                try
                {
                    string debugPath = Path.Combine(Path.GetTempPath(), "SmartHopper_WebChat_Debug.html");
                    File.WriteAllText(debugPath, completeHtml);
                    Debug.WriteLine($"[ChatResourceManager] Wrote complete HTML to debug file: {debugPath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatResourceManager] Failed to write debug file: {ex.Message}");
                }

                return completeHtml;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatResourceManager] Error creating complete HTML: {ex.Message}");
                Debug.WriteLine($"[ChatResourceManager] Error stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Gets the chat template HTML.
        /// </summary>
        /// <returns>The chat template HTML.</returns>
        private string GetChatTemplate()
        {
            Debug.WriteLine("[ChatResourceManager] Getting chat template");

            if (string.IsNullOrEmpty(_cachedChatTemplate))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {CHAT_TEMPLATE_RESOURCE}");
                _cachedChatTemplate = ReadEmbeddedResource(CHAT_TEMPLATE_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] Chat template loaded, length: {_cachedChatTemplate?.Length ?? 0}");
            }

            return _cachedChatTemplate;
        }

        /// <summary>
        /// Gets the message template HTML.
        /// </summary>
        /// <returns>The message template HTML.</returns>
        private string GetMessageTemplate()
        {
            Debug.WriteLine("[ChatResourceManager] Getting message template");

            if (string.IsNullOrEmpty(_cachedMessageTemplate))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {MESSAGE_TEMPLATE_RESOURCE}");
                _cachedMessageTemplate = ReadEmbeddedResource(MESSAGE_TEMPLATE_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] Message template loaded, length: {_cachedMessageTemplate?.Length ?? 0}");
            }

            return _cachedMessageTemplate;
        }

        /// <summary>
        /// Gets the error template HTML.
        /// </summary>
        /// <param name="errorMessage">The error message to display.</param>
        /// <returns>The error template HTML with the error message injected.</returns>
        public string GetErrorTemplate(string errorMessage)
        {
            Debug.WriteLine("[ChatResourceManager] Getting error template");

            if (string.IsNullOrEmpty(_cachedErrorTemplate))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {ERROR_TEMPLATE_RESOURCE}");
                _cachedErrorTemplate = ReadEmbeddedResource(ERROR_TEMPLATE_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] Error template loaded, length: {_cachedErrorTemplate?.Length ?? 0}");
            }

            // Replace error message placeholder
            string result = _cachedErrorTemplate.Replace("{{errorMessage}}", WebUtility.HtmlEncode(errorMessage));
            Debug.WriteLine("[ChatResourceManager] Error template prepared with error message injected");

            return result;
        }

        /// <summary>
        /// Gets the CSS content.
        /// </summary>
        /// <returns>The CSS content.</returns>
        private string GetCssContent()
        {
            Debug.WriteLine("[ChatResourceManager] Getting CSS content");

            if (string.IsNullOrEmpty(_cachedCssContent))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {CSS_RESOURCE}");
                _cachedCssContent = ReadEmbeddedResource(CSS_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] CSS content loaded, length: {_cachedCssContent?.Length ?? 0}");
            }

            return _cachedCssContent;
        }

        /// <summary>
        /// Gets the JavaScript content.
        /// </summary>
        /// <returns>The JavaScript content.</returns>
        private string GetJsContent()
        {
            Debug.WriteLine("[ChatResourceManager] Getting JavaScript content");

            if (string.IsNullOrEmpty(_cachedJsContent))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {JS_RESOURCE}");
                _cachedJsContent = ReadEmbeddedResource(JS_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] JavaScript content loaded, length: {_cachedJsContent?.Length ?? 0}");
            }

            return _cachedJsContent;
        }

        /// <summary>
        /// Renders the reasoning panel (if any) as a collapsible HTML details block.
        /// </summary>
        /// <param name="rawResponse">Raw markdown response including <think> tags.</param>
        /// <returns>HTML for reasoning panel or empty string.</returns>
        private string RenderReasoning(string rawResponse)
        {
            var m = Regex.Match(rawResponse, @"<think>([\s\S]*?)</think>", RegexOptions.Singleline);
            if (!m.Success) return "";
            var reasoningMd = m.Groups[1].Value;
            var reasoningHtml = Markdown.ToHtml(reasoningMd, _markdownPipeline);
            return $"<details class=\"think\"><summary>Reasoning</summary>{reasoningHtml}</details>";
        }

        /// <summary>
        /// Strips any <think>…</think> sections from the raw response.
        /// </summary>
        /// <param name="rawResponse">Raw markdown response including <think> tags.</param>
        /// <returns>Markdown string without reasoning sections.</returns>
        private string StripReasoning(string rawResponse)
        {
            return Regex.Replace(rawResponse, @"<think>[\s\S]*?</think>", "", RegexOptions.Singleline).Trim();
        }

        /// <summary>
        /// Creates a message HTML from the template.
        /// </summary>
        /// <param name="role">The role of the message sender (user, assistant, system).</param>
        /// <param name="displayName">The display name of the sender.</param>
        /// <param name="content">The HTML content of the message.</param>
        /// <param name="timestamp">The formatted timestamp of the message.</param>
        /// <param name="inTokens">Number of input tokens (for AI responses)</param>
        /// <param name="outTokens">Number of output tokens (for AI responses)</param>
        /// <param name="provider">AI provider name (for AI responses)</param>
        /// <param name="model">AI model name (for AI responses)</param>
        /// <param name="finishReason">AI response finish reason (for AI responses)</param>
        /// <returns>The HTML for the message.</returns>
        public string CreateMessageHtml(string role, string displayName, string timestamp, AIResponse response)
        {
            Debug.WriteLine($"[ChatResourceManager] Creating message HTML for role: {role}");

            // Convert markdown to HTML
            Debug.WriteLine("[ChatResourceManager] Converting markdown to HTML");
            var raw = response?.Response ?? "";
            var reasoningPanel = RenderReasoning(raw);
            var answerMd = StripReasoning(raw);
            Debug.WriteLine("[ChatResourceManager] Converting answer markdown to HTML");
            string answerHtml = Markdown.ToHtml(answerMd, _markdownPipeline);
            Debug.WriteLine($"[ChatResourceManager] Answer HTML length: {answerHtml?.Length ?? 0}");

            // Escape answer markdown for safe use in an HTML attribute
            string mdContentEscaped = System.Net.WebUtility.HtmlEncode(answerMd).Replace("'", "&#39;");

            string template = GetMessageTemplate();

            string result = template
                .Replace("{{role}}", role)
                .Replace("{{displayName}}", displayName)
                .Replace("{{timestamp}}", timestamp)
                .Replace("{{htmlContent}}", reasoningPanel + answerHtml)
                .Replace("{{mdContent}}", mdContentEscaped)
                .Replace("{{inTokens}}", response?.InTokens.ToString() ?? "")
                .Replace("{{outTokens}}", response?.OutTokens.ToString() ?? "")
                .Replace("{{provider}}", response?.Provider ?? "")
                .Replace("{{model}}", response?.Model ?? "")
                .Replace("{{finishReason}}", response?.FinishReason ?? "unknown");

            Debug.WriteLine($"[ChatResourceManager] Message HTML created, length: {result?.Length ?? 0}");

            return result;
        }

        /// <summary>
        /// Reads an embedded resource from the assembly.
        /// </summary>
        /// <param name="resourceName">The name of the resource to read.</param>
        /// <returns>The content of the resource as a string.</returns>
        private string ReadEmbeddedResource(string resourceName)
        {
            Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {resourceName}");

            Assembly assembly = Assembly.GetExecutingAssembly();

            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Debug.WriteLine($"[ChatResourceManager] ERROR: Embedded resource not found: {resourceName}");
                        throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
                    }

                    Debug.WriteLine($"[ChatResourceManager] Resource stream opened, length: {stream.Length}");

                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string content = reader.ReadToEnd();
                        Debug.WriteLine($"[ChatResourceManager] Resource read successfully, content length: {content?.Length ?? 0}");
                        return content;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatResourceManager] Error reading embedded resource: {ex.Message}");
                Debug.WriteLine($"[ChatResourceManager] Error stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Lists all embedded resources in the assembly for debugging purposes.
        /// </summary>
        private void ListAllEmbeddedResources()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string[] resources = assembly.GetManifestResourceNames();

                Debug.WriteLine($"[ChatResourceManager] Found {resources.Length} embedded resources:");
                foreach (string resource in resources)
                {
                    Debug.WriteLine($"[ChatResourceManager]   - {resource}");
                }

                // Check if our specific resources exist
                Debug.WriteLine("[ChatResourceManager] Checking for required resources:");
                Debug.WriteLine($"[ChatResourceManager]   - CSS: {resources.Contains(CSS_RESOURCE)}");
                Debug.WriteLine($"[ChatResourceManager]   - JS: {resources.Contains(JS_RESOURCE)}");
                Debug.WriteLine($"[ChatResourceManager]   - Chat Template: {resources.Contains(CHAT_TEMPLATE_RESOURCE)}");
                Debug.WriteLine($"[ChatResourceManager]   - Message Template: {resources.Contains(MESSAGE_TEMPLATE_RESOURCE)}");
                Debug.WriteLine($"[ChatResourceManager]   - Error Template: {resources.Contains(ERROR_TEMPLATE_RESOURCE)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatResourceManager] Error listing embedded resources: {ex.Message}");
            }
        }
    }
}
