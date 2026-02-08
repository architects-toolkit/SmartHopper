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

namespace SmartHopper.Providers.Anthropic
{
    /// <summary>
    /// Factory class for creating instances of the Anthropic provider and its settings.
    /// </summary>
    public class AnthropicProviderFactory : IAIProviderFactory
    {
        public IAIProvider CreateProvider()
        {
            return AnthropicProvider.Instance;
        }

        public IAIProviderSettings CreateProviderSettings()
        {
            return new AnthropicProviderSettings(AnthropicProvider.Instance);
        }
    }
}
