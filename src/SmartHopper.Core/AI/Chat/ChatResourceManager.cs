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
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace SmartHopper.Core.AI.Chat
{
    /// <summary>
    /// Manages resources for the chat interface, including HTML templates, CSS, and JavaScript.
    /// </summary>
    public class ChatResourceManager
    {
        private string _cachedChatTemplate;
        private string _cachedMessageTemplate;
        private string _cachedCssContent;
        private string _cachedJsContent;

        // Resource names
        private const string CSS_RESOURCE = "SmartHopper.Core.AI.Chat.Resources.css.chat-styles.css";
        private const string JS_RESOURCE = "SmartHopper.Core.AI.Chat.Resources.js.chat-script.js";
        private const string CHAT_TEMPLATE_RESOURCE = "SmartHopper.Core.AI.Chat.Resources.templates.chat-template.html";
        private const string MESSAGE_TEMPLATE_RESOURCE = "SmartHopper.Core.AI.Chat.Resources.templates.message-template.html";

        /// <summary>
        /// Initializes a new instance of the ChatResourceManager class.
        /// </summary>
        public ChatResourceManager()
        {
            Debug.WriteLine("[ChatResourceManager] Initializing ChatResourceManager");
            
            // List all embedded resources for debugging
            ListAllEmbeddedResources();
        }

        /// <summary>
        /// Gets the chat template HTML with CSS and JS paths replaced.
        /// </summary>
        /// <returns>The complete chat template HTML.</returns>
        public string GetChatTemplate()
        {
            Debug.WriteLine("[ChatResourceManager] Getting chat template");
            
            if (string.IsNullOrEmpty(_cachedChatTemplate))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {CHAT_TEMPLATE_RESOURCE}");
                _cachedChatTemplate = ReadEmbeddedResource(CHAT_TEMPLATE_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] Chat template loaded, length: {_cachedChatTemplate?.Length ?? 0}");
            }

            // Get the message template to inject
            string messageTemplate = GetMessageTemplate();
            
            // Escape single quotes in the message template to avoid breaking the JavaScript
            messageTemplate = messageTemplate.Replace("'", "\\'");
            Debug.WriteLine("[ChatResourceManager] Message template prepared for injection");

            // Replace placeholders with actual paths and content
            string result = _cachedChatTemplate
                .Replace("{{cssPath}}", "embedded-css") // Not used, but kept for compatibility
                .Replace("{{jsPath}}", "embedded-js") // Not used, but kept for compatibility
                .Replace("{{messageTemplate}}", messageTemplate);

            Debug.WriteLine("[ChatResourceManager] Chat template prepared with placeholders replaced");
            return result;
        }

        /// <summary>
        /// Gets the message template HTML.
        /// </summary>
        /// <returns>The message template HTML.</returns>
        public string GetMessageTemplate()
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
        /// Gets the CSS content.
        /// </summary>
        /// <returns>The CSS content.</returns>
        public string GetCssContent()
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
        public string GetJsContent()
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
        /// Creates a complete HTML document with embedded CSS and JS for offline use.
        /// </summary>
        /// <returns>The complete HTML document.</returns>
        public string GetCompleteHtml()
        {
            Debug.WriteLine("[ChatResourceManager] Creating complete HTML with embedded resources");
            
            try
            {
                string cssContent = GetCssContent();
                string jsContent = GetJsContent();
                string templateHtml = GetChatTemplate();

                // Replace CSS and JS path references with inline content
                string completeHtml = templateHtml
                    .Replace("<link rel=\"stylesheet\" href=\"{{cssPath}}\">", $"<style>\n{cssContent}\n</style>")
                    .Replace("<script src=\"{{jsPath}}\"></script>", $"<script>\n{jsContent}\n</script>");

                Debug.WriteLine($"[ChatResourceManager] Complete HTML created, length: {completeHtml?.Length ?? 0}");
                
                // For debugging, write the first 100 characters
                if (completeHtml != null && completeHtml.Length > 0)
                {
                    string preview = completeHtml.Length > 100 ? completeHtml.Substring(0, 100) + "..." : completeHtml;
                    Debug.WriteLine($"[ChatResourceManager] HTML preview: {preview}");
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
        /// Creates a message HTML from the template.
        /// </summary>
        /// <param name="role">The role of the message sender (user, assistant, system).</param>
        /// <param name="displayName">The display name of the sender.</param>
        /// <param name="content">The HTML content of the message.</param>
        /// <returns>The HTML for the message.</returns>
        public string CreateMessageHtml(string role, string displayName, string content)
        {
            Debug.WriteLine($"[ChatResourceManager] Creating message HTML for role: {role}");
            
            string template = GetMessageTemplate();

            string result = template
                .Replace("{{role}}", role)
                .Replace("{{displayName}}", displayName)
                .Replace("{{content}}", content);
                
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatResourceManager] Error listing embedded resources: {ex.Message}");
            }
        }
    }
}
