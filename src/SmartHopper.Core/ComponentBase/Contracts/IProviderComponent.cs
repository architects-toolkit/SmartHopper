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

using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Core.ComponentBase.Contracts
{
    /// <summary>
    /// Interface for Grasshopper components that provide AI provider selection functionality.
    /// </summary>
    public interface IProviderComponent
    {
        /// <summary>
        /// Gets the currently selected AI provider name.
        /// Can be "Default" to use the default provider from settings.
        /// </summary>
        string SelectedProviderName { get; }

        /// <summary>
        /// Sets the selected AI provider name.
        /// </summary>
        /// <param name="providerName">The provider name to set.</param>
        void SetSelectedProviderName(string providerName);

        /// <summary>
        /// Gets the actual provider name to use for AI processing.
        /// If the selected provider is "Default", returns the default provider from settings.
        /// </summary>
        /// <returns>The actual provider name to use.</returns>
        string GetActualAIProviderName();

        /// <summary>
        /// Gets the currently selected AI provider instance.
        /// </summary>
        /// <returns>The AI provider instance, or null if not available.</returns>
        AIProvider GetActualAIProvider();
    }
}
