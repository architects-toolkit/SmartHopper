/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.AICall.Core.Base
{
    /// <summary>
    /// Distinguishes between normal generation requests and provider backoffice/metadata requests.
    /// </summary>
    public enum AIRequestKind
    {
        /// <summary>
        /// Default request kind for content generation and normal AI operations.
        /// </summary>
        Generation = 0,

        /// <summary>
        /// Backoffice or metadata request (e.g., /models). Model/body checks are bypassed.
        /// </summary>
        Backoffice = 1,
    }
}
