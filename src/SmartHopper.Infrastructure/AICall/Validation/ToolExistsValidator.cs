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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Utilities;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall.Validation
{
    /// <summary>
    /// Validates that a tool call references an existing, registered tool.
    /// </summary>
    public sealed class ToolExistsValidator : IValidator<AIInteractionToolCall>
    {
        public AIRuntimeMessageSeverity FailOn { get; } = AIRuntimeMessageSeverity.Error;

        public Task<ValidationResult> ValidateAsync(AIInteractionToolCall instance, ValidationContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var messages = new List<AIRuntimeMessage>();

            if (instance == null)
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    "Tool call instance is null"));
            }
            else if (string.IsNullOrWhiteSpace(instance.Name))
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    "Tool name is required"));
            }
            else
            {
                var tools = AIToolManager.GetTools();
                if (!tools.ContainsKey(instance.Name))
                {
                    messages.Add(new AIRuntimeMessage(
                        AIRuntimeMessageSeverity.Error,
                        AIRuntimeMessageOrigin.Validation,
                        $"Unknown tool '{instance.Name}'"));
                }
            }

            var result = new ValidationResult
            {
                Messages = messages,
            };
            result.IsValid = !RuntimeMessageUtility.HasSeverityAtOrAbove(messages, this.FailOn);
            return Task.FromResult(result);
        }
    }
}
