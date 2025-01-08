/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Defines the contract for parallel processing operations.
 * This interface provides methods for handling concurrent operations
 * with proper resource management and throttling capabilities.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Interface for parallel processing operations. Implement this to provide
    /// concurrent processing capabilities with proper resource management.
    /// </summary>
    public interface IParallelProcessor
    {
        /// <summary>
        /// Processes a collection of items in parallel with proper resource management.
        /// Implement this to handle batch processing of items concurrently.
        /// </summary>
        /// <typeparam name="T">The type of items to process</typeparam>
        /// <param name="items">Collection of items to process</param>
        /// <param name="processor">The function to process each item</param>
        /// <returns>A task representing the parallel processing operation</returns>
        /// <remarks>
        /// Implementation should consider:
        /// - Maximum degree of parallelism
        /// - Resource constraints
        /// - Error handling for individual items
        /// - Progress reporting
        /// </remarks>
        Task ProcessInParallel<T>(IEnumerable<T> items, Func<T, Task> processor);

        /// <summary>
        /// Processes an input with throttling to prevent resource exhaustion.
        /// Implement this for operations that need rate limiting.
        /// </summary>
        /// <typeparam name="T">The type of input to process</typeparam>
        /// <typeparam name="TResult">The type of result</typeparam>
        /// <param name="input">The input to process</param>
        /// <param name="processor">The function to process the input</param>
        /// <returns>The result of the processing</returns>
        /// <remarks>
        /// Implementation should handle:
        /// - Rate limiting
        /// - Timeout management
        /// - Resource cleanup
        /// - Error recovery
        /// </remarks>
        Task<TResult> ProcessWithThrottling<T, TResult>(T input, Func<T, Task<TResult>> processor);

        // TODO: Consider adding these methods:
        // - Task<bool> IsResourceAvailable()
        // - void SetParallelismDegree(int degree)
        // - Task WaitForResourceAvailability(TimeSpan timeout)
    }
}
