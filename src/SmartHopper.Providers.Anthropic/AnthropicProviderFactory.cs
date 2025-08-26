/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
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
