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

namespace SmartHopper.ProviderSdk.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Logging;

    /// <summary>
    /// Generic AI provider used by SDK typed-logger tests.
    /// </summary>
    public sealed class FakeGenericAIProvider : AIProvider<FakeGenericAIProvider>
    {
        /// <summary>
        /// The canonical provider name used in tests.
        /// </summary>
        public const string ProviderName = "FakeGenericProvider";

        private FakeGenericAIProvider()
        {
        }

        /// <inheritdoc />
        public override string Name => ProviderName;

        /// <inheritdoc />
        public override Image Icon => new Bitmap(1, 1);

        /// <inheritdoc />
        public override bool IsEnabled => true;

        /// <inheritdoc />
        public override bool IsConfigured => true;

        /// <inheritdoc />
        public override Uri DefaultServerUrl => new Uri("https://fake.example/");

        /// <inheritdoc />
        public override string Encode(AIRequestCall request)
        {
            return "{}";
        }

        /// <inheritdoc />
        public override string Encode(IAIInteraction interaction)
        {
            return "{}";
        }

        /// <inheritdoc />
        public override List<IAIInteraction> Decode(JObject response)
        {
            return new List<IAIInteraction>();
        }

        /// <summary>
        /// Gets the typed logger for test assertions.
        /// </summary>
        public ILogger<FakeGenericAIProvider> ExposedLog => this.Log;
    }
}
