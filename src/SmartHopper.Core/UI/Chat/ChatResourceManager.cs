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

/*
 * Resource manager for the chat interface.
 * This class provides methods for loading and managing HTML, CSS, and JavaScript resources.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Manages resources for the chat interface, including HTML templates, CSS, and JavaScript.
    /// </summary>
    internal partial class ChatResourceManager
    {
        #region Compiled Regex Patterns

        /// <summary>
        /// Regex pattern for extracting content from think tags.
        /// </summary>
        [GeneratedRegex(@"<think>([\s\S]*?)</think>", RegexOptions.Singleline)]
        private static partial Regex ThinkTagRegex();

        #endregion
        private string _cachedChatTemplate;
        private string _cachedMessageTemplate;
        private string _cachedErrorTemplate;
        private string _cachedCssContent;
        private string _cachedJsContent;
        private readonly MarkdownPipeline _markdownPipeline;

        /// <summary>
        /// Per-render-identity cache of completed (closed) Markdown blocks and their rendered HTML.
        /// Used by <see cref="RenderMarkdownWithBlockCache"/> to avoid re-parsing and re-rendering
        /// the entire accumulated text on every streaming delta; only the trailing (still-growing)
        /// block is re-rendered on each call. Keyed by a caller-supplied stable identity (e.g. the
        /// DOM key of the message bubble plus a suffix identifying content vs. reasoning).
        /// </summary>
        private readonly ConcurrentDictionary<string, BlockCacheEntry[]> _blockCachesByKey = new (StringComparer.Ordinal);

        private readonly Queue<string> _blockCacheKeyOrder = new ();
        private readonly object _blockCacheLock = new ();
        private const int MaxBlockCacheKeys = 100;

        /// <summary>
        /// A single cached Markdown block: its exact source text and the HTML it was rendered to.
        /// </summary>
        private readonly struct BlockCacheEntry
        {
            public BlockCacheEntry(string source, string html)
            {
                this.Source = source;
                this.Html = html;
            }

            public string Source { get; }

            public string Html { get; }
        }

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
            this.ListAllEmbeddedResources();

            // Configure Markdig pipeline with needed extensions
            this._markdownPipeline = new MarkdownPipelineBuilder()
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
                string cssContent = this.GetCssContent();
                string jsContent = this.GetJsContent();
                string messageTemplate = this.GetMessageTemplate();
                string chatTemplate = this.GetChatTemplate();

                // Escape single quotes in the message template to avoid breaking the JavaScript
                messageTemplate = messageTemplate.Replace("'", "\\'", StringComparison.Ordinal);

                // Replace all placeholders with actual content
                string completeHtml = chatTemplate
                    .Replace("{{cssChat}}", cssContent, StringComparison.Ordinal)
                    .Replace("{{jsChat}}", jsContent, StringComparison.Ordinal)
                    .Replace("{{debugActionsLeft}}", this.GetDebugActionsLeftHtml(), StringComparison.Ordinal)
                    .Replace("{{messageTemplate}}", messageTemplate, StringComparison.Ordinal);

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

        private string GetDebugActionsLeftHtml()
        {
#if DEBUG
            return "<button id=\"regen-button\" type=\"button\">Regen</button><button id=\"update-button\" type=\"button\">Update</button>";
#else
            return string.Empty;
#endif
        }

        /// <summary>
        /// Gets the chat template HTML.
        /// </summary>
        /// <returns>The chat template HTML.</returns>
        private string GetChatTemplate()
        {
            Debug.WriteLine("[ChatResourceManager] Getting chat template");

            if (string.IsNullOrEmpty(this._cachedChatTemplate))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {CHAT_TEMPLATE_RESOURCE}");
                this._cachedChatTemplate = this.ReadEmbeddedResource(CHAT_TEMPLATE_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] Chat template loaded, length: {this._cachedChatTemplate?.Length ?? 0}");
            }

            return this._cachedChatTemplate;
        }

        /// <summary>
        /// Gets the message template HTML.
        /// </summary>
        /// <returns>The message template HTML.</returns>
        private string GetMessageTemplate()
        {
            Debug.WriteLine("[ChatResourceManager] Getting message template");

            if (string.IsNullOrEmpty(this._cachedMessageTemplate))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {MESSAGE_TEMPLATE_RESOURCE}");
                this._cachedMessageTemplate = this.ReadEmbeddedResource(MESSAGE_TEMPLATE_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] Message template loaded, length: {this._cachedMessageTemplate?.Length ?? 0}");
            }

            return this._cachedMessageTemplate;
        }

        /// <summary>
        /// Gets the error template HTML.
        /// </summary>
        /// <param name="errorMessage">The error message to display.</param>
        /// <returns>The error template HTML with the error message injected.</returns>
        public string GetErrorTemplate(string errorMessage)
        {
            Debug.WriteLine("[ChatResourceManager] Getting error template");

            if (string.IsNullOrEmpty(this._cachedErrorTemplate))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {ERROR_TEMPLATE_RESOURCE}");
                this._cachedErrorTemplate = this.ReadEmbeddedResource(ERROR_TEMPLATE_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] Error template loaded, length: {this._cachedErrorTemplate?.Length ?? 0}");
            }

            // Replace error message placeholder
            string result = this._cachedErrorTemplate.Replace("{{errorMessage}}", WebUtility.HtmlEncode(errorMessage), StringComparison.Ordinal);
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

            if (string.IsNullOrEmpty(this._cachedCssContent))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {CSS_RESOURCE}");
                this._cachedCssContent = this.ReadEmbeddedResource(CSS_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] CSS content loaded, length: {this._cachedCssContent?.Length ?? 0}");
            }

            return this._cachedCssContent;
        }

        /// <summary>
        /// Gets the JavaScript content.
        /// </summary>
        /// <returns>The JavaScript content.</returns>
        private string GetJsContent()
        {
            Debug.WriteLine("[ChatResourceManager] Getting JavaScript content");

            if (string.IsNullOrEmpty(this._cachedJsContent))
            {
                Debug.WriteLine($"[ChatResourceManager] Reading embedded resource: {JS_RESOURCE}");
                this._cachedJsContent = this.ReadEmbeddedResource(JS_RESOURCE);
                Debug.WriteLine($"[ChatResourceManager] JavaScript content loaded, length: {this._cachedJsContent?.Length ?? 0}");
            }

            return this._cachedJsContent;
        }

        /// <summary>
        /// Renders the reasoning panel (if any) as a collapsible HTML details block.
        /// Accepts either a raw response containing <think> tags or a plain reasoning string.
        /// </summary>
        /// <param name="reasoning">Reasoning text. May already be plain text (no <think>) or include <think>...</think> wrapper.</param>
        /// <param name="expand">If true, the panel will be rendered expanded (open attribute).</param>
        /// <param name="cacheKey">Stable identity for block-level render caching, or null to disable caching.</param>
        /// <returns>HTML for reasoning panel or empty string.</returns>
        private string RenderReasoning(string reasoning, bool expand, string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(reasoning)) return string.Empty;

            // If input contains <think>...</think>, extract inner content; otherwise use as-is
            string reasoningMd;
            var m = ThinkTagRegex().Match(reasoning);
            if (m.Success)
            {
                reasoningMd = m.Groups[1].Value;
            }
            else
            {
                reasoningMd = reasoning;
            }

            var reasoningHtml = this.RenderMarkdownWithBlockCache(reasoningMd, AppendCacheSuffix(cacheKey, "reasoning"));
            var openAttr = expand ? " open" : string.Empty;
            return $"<details class=\"think\"{openAttr}><summary>Reasoning</summary>{reasoningHtml}</details>";
        }

        /// <summary>
        /// Renders Markdown to HTML, reusing cached HTML for completed (closed) blocks so that only
        /// the trailing, still-growing block is re-parsed and re-rendered on each streaming delta.
        /// Falls back to a plain full-document render when no stable <paramref name="cacheKey"/> is
        /// supplied, or when block parsing fails for any reason.
        /// </summary>
        /// <param name="rawText">The raw Markdown source to render.</param>
        /// <param name="cacheKey">Stable identity for this piece of content across renders (e.g. a DOM key), or null to disable caching.</param>
        /// <returns>The rendered HTML.</returns>
        private string RenderMarkdownWithBlockCache(string rawText, string cacheKey)
        {
            if (string.IsNullOrEmpty(rawText))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                // No stable identity to cache against (e.g. one-shot renders); render directly.
                return Markdown.ToHtml(rawText, this._markdownPipeline);
            }

            List<(int Start, int Length)> blockSpans;
            try
            {
                blockSpans = GetTopLevelBlockSpans(rawText, this._markdownPipeline);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatResourceManager] Block parsing failed for cacheKey='{cacheKey}', falling back to full render: {ex.Message}");
                return Markdown.ToHtml(rawText, this._markdownPipeline);
            }

            if (blockSpans.Count == 0)
            {
                return Markdown.ToHtml(rawText, this._markdownPipeline);
            }

            var previousBlocks = this._blockCachesByKey.TryGetValue(cacheKey, out var cached) ? cached : Array.Empty<BlockCacheEntry>();

            // All blocks except the last are "closed": the parser only emits a following block once
            // the previous one can no longer change, so their rendered HTML can be safely reused.
            int completedCount = blockSpans.Count - 1;
            var newCompletedBlocks = new BlockCacheEntry[completedCount];
            var htmlBuilder = new StringBuilder();

            for (int i = 0; i < completedCount; i++)
            {
                var (start, length) = blockSpans[i];
                var source = rawText.Substring(start, length);

                string html = (i < previousBlocks.Length && string.Equals(previousBlocks[i].Source, source, StringComparison.Ordinal))
                    ? previousBlocks[i].Html
                    : Markdown.ToHtml(source, this._markdownPipeline);

                newCompletedBlocks[i] = new BlockCacheEntry(source, html);
                htmlBuilder.Append(html);
            }

            // Always re-render the trailing block: it may still be growing mid-stream.
            // Repair dangling inline syntax first so a partial token (e.g. "**bold") does not
            // flash as literal text for the moment before its closing marker arrives.
            var (lastStart, lastLength) = blockSpans[^1];
            var lastSource = rawText.Substring(lastStart, lastLength);
            htmlBuilder.Append(Markdown.ToHtml(RepairIncompleteMarkdownTail(lastSource), this._markdownPipeline));

            this.StoreBlockCache(cacheKey, newCompletedBlocks);

            return htmlBuilder.ToString();
        }

        /// <summary>
        /// Parses <paramref name="rawText"/> and returns the character span (start, length) of each
        /// top-level Markdown block (paragraph, heading, list, fenced code block, table, etc.).
        /// </summary>
        private static List<(int Start, int Length)> GetTopLevelBlockSpans(string rawText, MarkdownPipeline pipeline)
        {
            var document = MarkdownParser.Parse(rawText, pipeline);
            var spans = new List<(int Start, int Length)>(document.Count);

            foreach (var block in document)
            {
                var span = block.Span;
                if (span.Start < 0 || span.End < span.Start)
                {
                    continue;
                }

                int start = span.Start;
                int end = Math.Min(span.End, rawText.Length - 1);
                int length = end - start + 1;
                if (length <= 0)
                {
                    continue;
                }

                spans.Add((start, length));
            }

            return spans;
        }

        /// <summary>
        /// Temporarily closes dangling inline Markdown syntax (unterminated fenced code blocks,
        /// bold markers, and inline code spans) so a still-streaming block never renders as raw,
        /// unformatted text for the brief moment before its closing marker arrives.
        /// The original source is never mutated; this only affects what gets rendered to HTML.
        /// </summary>
        /// <param name="text">The trailing (still-growing) block's Markdown source.</param>
        /// <returns>The text with any dangling syntax closed for rendering purposes.</returns>
        private static string RepairIncompleteMarkdownTail(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Unterminated fenced code block: close it so the block renders as code instead of
            // leaking the fence marker and any code syntax (e.g. "**", "`") as literal text.
            if (CountOccurrences(text, "```") % 2 != 0)
            {
                bool needsNewline = !text.EndsWith("\n", StringComparison.Ordinal);
                return text + (needsNewline ? "\n" : string.Empty) + "```";
            }

            // Not inside an open fence: guard against dangling inline markers.
            string repaired = text;

            if (CountOccurrences(repaired, "**") % 2 != 0)
            {
                repaired += "**";
            }

            if (CountChar(repaired, '`') % 2 != 0)
            {
                repaired += "`";
            }

            return repaired;
        }

        /// <summary>
        /// Counts non-overlapping occurrences of <paramref name="token"/> in <paramref name="text"/>.
        /// </summary>
        private static int CountOccurrences(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        /// <summary>
        /// Counts occurrences of a single character in <paramref name="text"/>.
        /// </summary>
        private static int CountChar(string text, char c)
        {
            int count = 0;
            foreach (var ch in text)
            {
                if (ch == c)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Stores the completed-block cache for a given identity, with simple LRU eviction to bound
        /// memory usage across long-running chat sessions.
        /// </summary>
        private void StoreBlockCache(string cacheKey, BlockCacheEntry[] completedBlocks)
        {
            try
            {
                lock (this._blockCacheLock)
                {
                    bool isExisting = this._blockCachesByKey.ContainsKey(cacheKey);
                    this._blockCachesByKey[cacheKey] = completedBlocks;

                    if (!isExisting)
                    {
                        this._blockCacheKeyOrder.Enqueue(cacheKey);
                        while (this._blockCacheKeyOrder.Count > MaxBlockCacheKeys)
                        {
                            var oldest = this._blockCacheKeyOrder.Dequeue();
                            this._blockCachesByKey.TryRemove(oldest, out _);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatResourceManager] StoreBlockCache error: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears all cached Markdown blocks (e.g. when regenerating the entire chat view).
        /// </summary>
        public void ClearBlockCaches()
        {
            lock (this._blockCacheLock)
            {
                this._blockCachesByKey.Clear();
                this._blockCacheKeyOrder.Clear();
            }
        }

        /// <summary>
        /// Appends a fixed suffix to a cache key so content and reasoning caches for the same
        /// message do not collide with each other.
        /// </summary>
        private static string AppendCacheSuffix(string cacheKey, string suffix)
        {
            return string.IsNullOrWhiteSpace(cacheKey) ? null : $"{cacheKey}:{suffix}";
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
        /// <param name="cacheKey">Stable identity for block-level render caching (e.g. the message's DOM key), or null to disable caching.</param>
        /// <returns>The HTML for the message.</returns>
        public string CreateMessageHtml(string timestamp, IAIInteraction interaction, string cacheKey = null)
        {
            // Get content and reasoning from interaction via IAIRenderInteraction when available
            string rawContent = string.Empty;
            string rawReasoning = string.Empty;
            string roleClass = string.Empty;
            string displayName = string.Empty;
            string provider = string.Empty;
            string model = string.Empty;
            string finishReason = "unknown";
            int inTokens = 0;
            int outTokens = 0;

            if (interaction is IAIRenderInteraction renderable)
            {
                rawContent = renderable.GetRawContentForRender() ?? string.Empty;
                rawReasoning = renderable.GetRawReasoningForRender() ?? string.Empty;
                roleClass = renderable.GetRoleClassForRender();
                displayName = renderable.GetDisplayNameForRender();
            }
            else
            {
                rawContent = interaction?.ToString() ?? string.Empty;
                rawReasoning = string.Empty;
                roleClass = interaction.Agent.ToString().ToLowerInvariant();
                displayName = interaction.Agent.ToDescription();
            }

            Debug.WriteLine($"[ChatResourceManager] Creating message HTML for role='{roleClass}', displayName='{displayName}', timestamp='{timestamp}'");

            // Extract metrics if available
            string contextUsage = string.Empty;
            if (interaction.Metrics != null)
            {
                provider = interaction.Metrics.Provider ?? string.Empty;
                model = interaction.Metrics.Model ?? string.Empty;
                finishReason = interaction.Metrics.FinishReason ?? "unknown";
                inTokens = interaction.Metrics.InputTokens;
                outTokens = interaction.Metrics.OutputTokens;

                // Format context usage as percentage if available
                if (interaction.Metrics.ContextUsagePercent.HasValue)
                {
                    contextUsage = $"{interaction.Metrics.ContextUsagePercent.Value * 100:F1}%";
                }
            }

            // Decide whether to show metrics icon. Hide when metrics are missing or not meaningful (e.g., during streaming).
            // Consider metrics meaningful if we have token counts or provider+model info.
            bool hasMeaningfulMetrics =
                (interaction.Metrics != null) &&
                ((inTokens > 0) || (outTokens > 0) ||
                 (!string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(model)));
            string metricsClass = hasMeaningfulMetrics ? string.Empty : "hidden";
            Debug.WriteLine($"[ChatResourceManager] Metrics visibility: {(hasMeaningfulMetrics ? "show" : "hide")}; in={inTokens}, out={outTokens}, provider='{provider}', model='{model}', reason='{finishReason}'");

            // Convert markdown to HTML
            Debug.WriteLine("[ChatResourceManager] Converting markdown to HTML");

            // Render reasoning panel. Auto-expand when there is no visible answer content.
            var reasoningPanel = this.RenderReasoning(rawReasoning, string.IsNullOrWhiteSpace(rawContent), cacheKey);
            Debug.WriteLine("[ChatResourceManager] Converting answer markdown to HTML");
            string answerHtml = this.RenderMarkdownWithBlockCache(rawContent, AppendCacheSuffix(cacheKey, "content"));
            Debug.WriteLine($"[ChatResourceManager] Answer HTML length: {answerHtml?.Length ?? 0}");

            // Escape answer markdown for safe use in an HTML attribute
            string mdContentEscaped = System.Net.WebUtility.HtmlEncode(rawContent).Replace("'", "&#39;", StringComparison.Ordinal);

            string template = this.GetMessageTemplate();

            string result = template
                .Replace("{{role}}", roleClass, StringComparison.Ordinal)
                .Replace("{{displayName}}", displayName, StringComparison.Ordinal)
                .Replace("{{timestamp}}", timestamp, StringComparison.Ordinal)
                .Replace("{{htmlContent}}", reasoningPanel + answerHtml, StringComparison.Ordinal)
                .Replace("{{mdContent}}", mdContentEscaped, StringComparison.Ordinal)
                .Replace("{{inTokens}}", inTokens.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
                .Replace("{{outTokens}}", outTokens.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
                .Replace("{{provider}}", provider, StringComparison.Ordinal)
                .Replace("{{model}}", model, StringComparison.Ordinal)
                .Replace("{{finishReason}}", finishReason, StringComparison.Ordinal)
                .Replace("{{contextUsage}}", contextUsage, StringComparison.Ordinal)
                .Replace("{{metricsClass}}", metricsClass, StringComparison.Ordinal);

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
