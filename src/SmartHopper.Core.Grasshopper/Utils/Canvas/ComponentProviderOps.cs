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
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Non-solving UI-thread provider/model mutations shared by
    /// <c>gh_component_set_provider</c> and <c>gh_apply_changes</c>: both call
    /// the same core so a composite commit and the standalone tool cannot
    /// diverge. The caller owns scheduling the downstream solution.
    /// </summary>
    internal static class ComponentProviderOps
    {
        /// <summary>
        /// Applies a provider and/or model change on the UI thread without
        /// requesting a solution. When <paramref name="model"/> is supplied a new
        /// panel is created and the Settings input is rewired to it — undo
        /// records are captured for the component state, the Settings rewiring
        /// and the added panel.
        /// </summary>
        /// <param name="ghComp">The target component.</param>
        /// <param name="providerComp">The component's provider contract.</param>
        /// <param name="provider">Normalized provider name, or null to leave unchanged.</param>
        /// <param name="model">Model name for the new Settings panel, or null.</param>
        /// <param name="settingsParam">Resolved Settings input (required when model is set).</param>
        /// <param name="doc">The owning document.</param>
        /// <returns>The outcome including a compensation inverse.</returns>
        internal static SetProviderOutcome ApplySetProviderOnUiThread(
            GH_Component ghComp,
            IProviderComponent providerComp,
            string? provider,
            string? model,
            IGH_Param? settingsParam,
            GH_Document doc)
        {
            var inverse = new SetProviderInverse
            {
                Component = ghComp,
                ProviderComponent = providerComp,
                PreviousProviderName = providerComp.SelectedProviderName,
                SettingsParam = settingsParam,
                PreviousSettingsSources = settingsParam?.Sources.ToArray(),
            };

            var undo = doc.UndoUtil.CreateGenericObjectEvent(
                "[SH] Set AI provider and model",
                ghComp);

            Guid? panelGuid = null;
            var providerSet = false;

            if (!string.IsNullOrWhiteSpace(provider))
            {
                providerComp.SetSelectedProviderName(provider);
                providerSet = true;
            }

            if (!string.IsNullOrWhiteSpace(model) && settingsParam != null)
            {
                var inputBounds = settingsParam.Attributes?.Bounds ?? ghComp.Attributes.Bounds;

                var panel = new GH_Panel()
                {
                    NickName = "Model",
                    UserText = model,
                };

                panel.CreateAttributes();

                // Start with a rough left-aligned position and measure the panel bounds,
                // then align the panel's right edge to the left of the Settings input
                // and vertically center it with the input.
                panel.Attributes.Pivot = new PointF(inputBounds.X - 220, inputBounds.Y);
                panel.Attributes.ExpireLayout();

                var panelBounds = panel.Attributes.Bounds;
                if (panelBounds.Width > 0 && panelBounds.Height > 0)
                {
                    float x = inputBounds.X - panelBounds.Width - 20;
                    float y = inputBounds.Y + ((inputBounds.Height - panelBounds.Height) / 2);
                    panel.Attributes.Pivot = new PointF(x, y);
                    panel.Attributes.ExpireLayout();
                }

                doc.AddObject(panel, false);

                settingsParam.RecordUndoEvent(undo);
                settingsParam.RemoveAllSources();
                settingsParam.AddSource(panel);

                panelGuid = panel.InstanceGuid;
                inverse.AddedPanel = panel;
            }

            doc.UndoUtil.RecordEvent(undo);

            if (inverse.AddedPanel != null)
            {
                doc.UndoUtil.RecordAddObjectEvent("[SH] Add model panel", inverse.AddedPanel);
            }

            ghComp.ExpireSolution(true);

            return new SetProviderOutcome
            {
                Success = true,
                ProviderSet = providerSet,
                PanelGuid = panelGuid,
                Inverse = inverse,
            };
        }

        /// <summary>
        /// Reverts a completed provider/model apply: restores the previous provider
        /// selection, restores the Settings input's previous sources and removes
        /// the added model panel without recording new undo events (the original
        /// apply records remain the user's ctrl+z history).
        /// </summary>
        /// <param name="inverse">The captured inverse.</param>
        /// <param name="doc">The owning document.</param>
        internal static void CompensateSetProviderOnUiThread(SetProviderInverse inverse, GH_Document doc)
        {
            if (inverse.SettingsParam != null && inverse.PreviousSettingsSources != null)
            {
                var settings = inverse.SettingsParam;
                var wanted = inverse.PreviousSettingsSources.ToHashSet();
                foreach (var source in settings.Sources.ToList())
                {
                    if (!wanted.Contains(source))
                    {
                        settings.RemoveSource(source);
                    }
                }

                foreach (var source in inverse.PreviousSettingsSources)
                {
                    if (!settings.Sources.Contains(source))
                    {
                        settings.AddSource(source);
                    }
                }
            }

            if (inverse.AddedPanel != null)
            {
                inverse.AddedPanel.IsolateObject();
                doc.RemoveObject(inverse.AddedPanel, false);
            }

            if (!string.IsNullOrWhiteSpace(inverse.PreviousProviderName))
            {
                inverse.ProviderComponent.SetSelectedProviderName(inverse.PreviousProviderName);
            }

            inverse.Component.ExpireSolution(true);
        }

        /// <summary>
        /// Captured pre-state for compensation: restores the provider selection,
        /// removes the added model panel and rewires the Settings input to its
        /// previous sources.
        /// </summary>
        internal sealed class SetProviderInverse
        {
            required public GH_Component Component { get; init; }

            required public IProviderComponent ProviderComponent { get; init; }

            public string? PreviousProviderName { get; init; }

            public IGH_Param? SettingsParam { get; init; }

            public IGH_Param[]? PreviousSettingsSources { get; init; }

            public GH_Panel? AddedPanel { get; set; }
        }

        /// <summary>
        /// Result of <see cref="ApplySetProviderOnUiThread"/>.
        /// </summary>
        internal sealed class SetProviderOutcome
        {
            public bool Success { get; init; }

            public string? Error { get; init; }

            public Guid? PanelGuid { get; init; }

            public bool ProviderSet { get; init; }

            public SetProviderInverse? Inverse { get; init; }
        }
    }
}
