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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Infrastructure.AICall.Policies.Request
{
    /// <summary>
    /// Injects a context interaction immutably at the beginning of the request body based on ContextFilter.
    /// Mirrors the legacy dynamic injection previously done by AIBody.Interactions getter,
    /// but as an explicit immutable transformation in the request policy phase.
    /// </summary>
    public sealed class ContextInjectionRequestPolicy : IRequestPolicy
    {
        public Task ApplyAsync(PolicyContext context)
        {
            var rq = context?.Request;
            var body = rq?.Body;
            if (rq == null || body == null)
            {
                return Task.CompletedTask;
            }

            var filter = body.ContextFilter;
            if (string.IsNullOrWhiteSpace(filter) || filter == "-*")
            {
                // No context requested
                return Task.CompletedTask;
            }

            // Collect current context according to filter
            var data = AIContextManager.GetCurrentContext(filter);
            var items = data
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .ToList();

            if (items.Count == 0)
            {
                return Task.CompletedTask;
            }

            // Build the context message (same format as legacy AIBody)
            var sb = new StringBuilder();
            sb.Append("Conversation context:\n\n");
            foreach (var kv in items)
            {
                sb.Append("- ");
                sb.Append(kv.Key);
                sb.Append(": ");
                sb.AppendLine(kv.Value);
            }

            var contextInteraction = new AIInteractionText
            {
                Agent = AIAgent.Context,
                Content = sb.ToString(),
            };

            // Rebuild body immutably with context as first item and filter-out any pre-existing context interactions
            var rest = body.Interactions?.Where(i => i != null && i.Agent != AIAgent.Context) ?? Enumerable.Empty<IAIInteraction>();

            var builder = AIBodyBuilder.Create()
                .WithToolFilter(body.ToolFilter)
                .WithContextFilter(body.ContextFilter)
                .WithJsonOutputSchema(body.JsonOutputSchema)
                .Add(contextInteraction)
                .AddRange(rest);

            rq.Body = builder.Build();
            return Task.CompletedTask;
        }
    }
}
