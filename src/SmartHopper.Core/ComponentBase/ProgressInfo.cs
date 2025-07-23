/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Represents progress information for component processing operations.
    /// </summary>
    public class ProgressInfo
    {
        /// <summary>
        /// Gets or sets the current item being processed (1-based).
        /// </summary>
        public int Current { get; set; }

        /// <summary>
        /// Gets or sets the total number of items to process.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Gets a value indicating whether progress tracking is active.
        /// </summary>
        public bool IsActive => this.Total > 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressInfo"/> class.
        /// </summary>
        public ProgressInfo()
        {
            this.Current = 0;
            this.Total = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressInfo"/> class.
        /// </summary>
        /// <param name="total">The total number of items to process.</param>
        public ProgressInfo(int total)
        {
            this.Current = total > 0 ? 1 : 0;
            this.Total = total;
        }

        /// <summary>
        /// Updates the current progress.
        /// </summary>
        /// <param name="current">The current item being processed (1-based).</param>
        public void UpdateCurrent(int current)
        {
            this.Current = current <= this.Total ? current : this.Total;
        }

        /// <summary>
        /// Resets the progress information.
        /// </summary>
        public void Reset()
        {
            this.Current = 0;
            this.Total = 0;
        }

        /// <summary>
        /// Gets a formatted progress string.
        /// </summary>
        /// <returns>A string like "1/3" if progress is active, otherwise empty string.</returns>
        public string GetProgressString()
        {
            return this.IsActive ? $"{this.Current}/{this.Total}" : string.Empty;
        }
    }
}
