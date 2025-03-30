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
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Manages resources for the chat interface, including HTML templates, CSS, and JavaScript.
    /// </summary>
    public class ChatResourceManager
    {
        private readonly string _resourceBasePath;
        private string _cachedChatTemplate;
        private string _cachedMessageTemplate;
        private string _cachedCssContent;
        private string _cachedJsContent;

        /// <summary>
        /// Initializes a new instance of the ChatResourceManager class.
        /// </summary>
        public ChatResourceManager()
        {
            // Get the base path for resources relative to the executing assembly
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            _resourceBasePath = Path.Combine(assemblyDirectory, "AI", "Chat", "Resources");
            
            // Ensure resource directories exist
            EnsureResourceDirectoriesExist();
        }

        /// <summary>
        /// Gets the path to the CSS file.
        /// </summary>
        public string CssPath => Path.Combine(_resourceBasePath, "css", "chat-styles.css");

        /// <summary>
        /// Gets the path to the JavaScript file.
        /// </summary>
        public string JsPath => Path.Combine(_resourceBasePath, "js", "chat-script.js");

        /// <summary>
        /// Gets the path to the chat template file.
        /// </summary>
        public string ChatTemplatePath => Path.Combine(_resourceBasePath, "templates", "chat-template.html");

        /// <summary>
        /// Gets the path to the message template file.
        /// </summary>
        public string MessageTemplatePath => Path.Combine(_resourceBasePath, "templates", "message-template.html");

        /// <summary>
        /// Ensures that the resource directories exist.
        /// </summary>
        private void EnsureResourceDirectoriesExist()
        {
            string cssDir = Path.Combine(_resourceBasePath, "css");
            string jsDir = Path.Combine(_resourceBasePath, "js");
            string templatesDir = Path.Combine(_resourceBasePath, "templates");

            Directory.CreateDirectory(cssDir);
            Directory.CreateDirectory(jsDir);
            Directory.CreateDirectory(templatesDir);
        }

        /// <summary>
        /// Gets the chat template HTML with CSS and JS paths replaced.
        /// </summary>
        /// <returns>The complete chat template HTML.</returns>
        public string GetChatTemplate()
        {
            if (string.IsNullOrEmpty(_cachedChatTemplate))
            {
                _cachedChatTemplate = File.ReadAllText(ChatTemplatePath);
            }

            // Get the message template to inject
            string messageTemplate = GetMessageTemplate();
            
            // Escape single quotes in the message template to avoid breaking the JavaScript
            messageTemplate = messageTemplate.Replace("'", "\\'");

            // Replace placeholders with actual paths and content
            string result = _cachedChatTemplate
                .Replace("{{cssPath}}", CssPath)
                .Replace("{{jsPath}}", JsPath)
                .Replace("{{messageTemplate}}", messageTemplate);

            return result;
        }

        /// <summary>
        /// Gets the message template HTML.
        /// </summary>
        /// <returns>The message template HTML.</returns>
        public string GetMessageTemplate()
        {
            if (string.IsNullOrEmpty(_cachedMessageTemplate))
            {
                _cachedMessageTemplate = File.ReadAllText(MessageTemplatePath);
            }

            return _cachedMessageTemplate;
        }

        /// <summary>
        /// Gets the CSS content.
        /// </summary>
        /// <returns>The CSS content.</returns>
        public string GetCssContent()
        {
            if (string.IsNullOrEmpty(_cachedCssContent))
            {
                _cachedCssContent = File.ReadAllText(CssPath);
            }

            return _cachedCssContent;
        }

        /// <summary>
        /// Gets the JavaScript content.
        /// </summary>
        /// <returns>The JavaScript content.</returns>
        public string GetJsContent()
        {
            if (string.IsNullOrEmpty(_cachedJsContent))
            {
                _cachedJsContent = File.ReadAllText(JsPath);
            }

            return _cachedJsContent;
        }

        /// <summary>
        /// Creates a complete HTML document with embedded CSS and JS for offline use.
        /// </summary>
        /// <returns>The complete HTML document.</returns>
        public string GetCompleteHtml()
        {
            string cssContent = GetCssContent();
            string jsContent = GetJsContent();
            string templateHtml = GetChatTemplate();

            // Replace CSS and JS path references with inline content
            string completeHtml = templateHtml
                .Replace("<link rel=\"stylesheet\" href=\"{{cssPath}}\">", $"<style>\n{cssContent}\n</style>")
                .Replace("<script src=\"{{jsPath}}\"></script>", $"<script>\n{jsContent}\n</script>");

            return completeHtml;
        }

        /// <summary>
        /// Creates a message HTML from the template.
        /// </summary>
        /// <param name="role">The role of the message sender (user, assistant, system).</param>
        /// <param name="displayName">The display name of the sender.</param>
        /// <param name="content">The HTML content of the message.</param>
        /// <returns>The HTML for the message.</returns>
        public string CreateMessageHtml(string role, string displayName, string content)
        {
            string template = GetMessageTemplate();

            return template
                .Replace("{{role}}", role)
                .Replace("{{displayName}}", displayName)
                .Replace("{{content}}", content);
        }
    }
}
