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
using System.Linq;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Builder for composing system prompts from multiple sections.
    /// Centralizes system-prompt composition with automatic omission of null/empty sections.
    /// </summary>
    public sealed class SystemPromptBuilder
    {
        private readonly List<(string name, string content)> _sections = new List<(string, string)>();
        private readonly HashSet<string> _contextProviderFilters = new HashSet<string>();

        /// <summary>
        /// Adds the internal/component-defined system prompt as the first section.
        /// This section cannot be overridden and is always placed first.
        /// </summary>
        /// <param name="internalPrompt">The internal system prompt defined by the component.</param>
        /// <returns>This builder for method chaining.</returns>
        public SystemPromptBuilder WithInternalPrompt(string internalPrompt)
        {
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder] WithInternalPrompt called");
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Input length: {internalPrompt?.Length ?? 0} chars");
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Input is null/whitespace: {string.IsNullOrWhiteSpace(internalPrompt)}");

            if (!string.IsNullOrWhiteSpace(internalPrompt))
            {
                this._sections.Insert(0, ("internal", internalPrompt));
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Added internal prompt section. Total sections now: {this._sections.Count}");
            }
            else
            {
                Logger.For<SystemPromptBuilder>().Warning($"[SystemPromptBuilder]   Skipped internal prompt (null/whitespace)");
            }

            return this;
        }

        /// <summary>
        /// Adds context provider filters to be retrieved during build.
        /// Can be called multiple times with individual provider IDs or multiple IDs at once.
        /// Context is retrieved from AIContextManager at build time, not at call time.
        /// </summary>
        /// <param name="providerIds">One or more provider IDs to include (e.g., "file", "rhino", "time").
        /// All specified providers will be included when context is built.</param>
        /// <returns>This builder for method chaining.</returns>
        public SystemPromptBuilder WithContext(params string[] providerIds)
        {
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder] WithContext called");
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Provider IDs count: {providerIds?.Length ?? 0}");

            if (providerIds != null && providerIds.Length > 0)
            {
                foreach (var providerId in providerIds)
                {
                    Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Processing provider ID: '{providerId}'");
                    if (!string.IsNullOrWhiteSpace(providerId))
                    {
                        var trimmed = providerId.Trim();
                        this._contextProviderFilters.Add(trimmed);
                        Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]     Added filter: '{trimmed}'. Total filters now: {this._contextProviderFilters.Count}");
                    }
                    else
                    {
                        Logger.For<SystemPromptBuilder>().Warning($"[SystemPromptBuilder]     Skipped empty/whitespace provider ID");
                    }
                }
            }
            else
            {
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   No provider IDs provided");
            }

            return this;
        }

        /// <summary>
        /// Adds user-provided instructions to augment the system prompt.
        /// </summary>
        /// <param name="userInstructions">User-provided instructions.</param>
        /// <returns>This builder for method chaining.</returns>
        public SystemPromptBuilder WithUserInstructions(string userInstructions)
        {
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder] WithUserInstructions(string) called");
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Input length: {userInstructions?.Length ?? 0} chars");
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Input is null/whitespace: {string.IsNullOrWhiteSpace(userInstructions)}");

            if (!string.IsNullOrWhiteSpace(userInstructions))
            {
                this._sections.Add(("user", userInstructions));
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Added user instructions section. Total sections now: {this._sections.Count}");
            }
            else
            {
                Logger.For<SystemPromptBuilder>().Warning($"[SystemPromptBuilder]   Skipped user instructions (null/whitespace)");
            }

            return this;
        }

        /// <summary>
        /// Adds multiple user-provided instruction interactions to augment the system prompt.
        /// Multiple instructions are concatenated in order.
        /// </summary>
        /// <param name="userInstructions">List of user instruction interactions (AIAgent.System).</param>
        /// <returns>This builder for method chaining.</returns>
        public SystemPromptBuilder WithUserInstructions(IEnumerable<AIInteractionText> userInstructions)
        {
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder] WithUserInstructions(IEnumerable<AIInteractionText>) called");

            if (userInstructions == null)
            {
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Input is null, returning");
                return this;
            }

            var instructionTexts = userInstructions
                .Where(i => !string.IsNullOrWhiteSpace(i.Content))
                .Select(i => i.Content)
                .ToList();

            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Found {instructionTexts.Count} non-empty instruction texts");

            if (instructionTexts.Count > 0)
            {
                var mergedInstructions = string.Join("\n\n", instructionTexts);
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Merged instructions length: {mergedInstructions.Length} chars");
                this._sections.Add(("user", mergedInstructions));
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Added merged user instructions section. Total sections now: {this._sections.Count}");
            }
            else
            {
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   No non-empty instruction texts found");
            }

            return this;
        }

        /// <summary>
        /// Builds the final system prompt by joining all non-null/non-empty sections.
        /// Retrieves context from AIContextManager at build time using collected provider filters.
        /// Sections are joined with "\n---\n" separator.
        /// </summary>
        /// <returns>The composed system prompt string.</returns>
        public string Build()
        {
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder] Build() called");
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Total sections registered: {this._sections.Count}");

            // Debug: Log all sections
            for (int i = 0; i < this._sections.Count; i++)
            {
                var (name, content) = this._sections[i];
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Section[{i}] name='{name}', length={content?.Length ?? 0}, empty={string.IsNullOrWhiteSpace(content)}");
                if (!string.IsNullOrWhiteSpace(content) && content.Length <= 100)
                {
                    Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]     Content preview: {content}");
                }
            }

            var allSections = new List<string>();

            // Add all pre-built sections
            var validSections = this._sections
                .Where(s => !string.IsNullOrWhiteSpace(s.content))
                .Select(s => s.content)
                .ToList();

            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Valid sections after filtering: {validSections.Count}");
            allSections.AddRange(validSections);

            // Retrieve and add context at build time if any provider filters were specified
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Context provider filters count: {this._contextProviderFilters.Count}");

            if (this._contextProviderFilters.Count > 0)
            {
                var providerFilter = string.Join(" ", this._contextProviderFilters);
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Requesting context with filter: '{providerFilter}'");

                var contextDict = ProviderSdkHost.ContextProvider.GetCurrentContext(providerFilter);

                if (contextDict != null)
                {
                    Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Context retrieved: {contextDict.Count} items");

                    if (contextDict.Count > 0)
                    {
                        var contextLines = contextDict
                            .Select(kvp => $"{kvp.Key}: {kvp.Value}")
                            .ToList();

                        var contextContent = string.Join("\n", contextLines);
                        Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Context content length: {contextContent.Length} chars");

                        if (!string.IsNullOrWhiteSpace(contextContent))
                        {
                            allSections.Add(contextContent);
                            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Added context section. Total sections now: {allSections.Count}");
                        }
                    }
                    else
                    {
                        Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Context dict is empty");
                    }
                }
                else
                {
                    Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Context dict is null");
                }
            }
            else
            {
                Logger.For<SystemPromptBuilder>().Warning($"[SystemPromptBuilder]   No context filters specified, skipping context retrieval");
            }

            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Final section count: {allSections.Count}");

            if (allSections.Count == 0)
            {
                Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   No sections to join, returning empty string");
                return string.Empty;
            }

            var result = string.Join("\n---\n", allSections);
            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Final prompt length: {result.Length} chars");

            Logger.For<SystemPromptBuilder>().Trace($"[SystemPromptBuilder]   Build complete; content omitted from logs");

            return result;
        }
    }
}
