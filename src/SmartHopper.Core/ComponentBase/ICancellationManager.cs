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
 * Defines the contract for managing cancellation operations.
 * This service handles the coordination of cancellation requests
 * and token management across asynchronous operations.
 */

using System.Threading;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Interface for cancellation management. Implement this to handle
    /// cancellation requests in your asynchronous components.
    /// </summary>
    public interface ICancellationManager
    {
        /// <summary>
        /// Gets the current cancellation token.
        /// Use this token to coordinate cancellation across async operations.
        /// </summary>
        /// <remarks>
        /// Implementation should:
        /// - Ensure thread safety
        /// - Handle token linking if needed
        /// - Manage token lifetime
        /// </remarks>
        CancellationToken Token { get; }

        /// <summary>
        /// Requests cancellation of the current operation.
        /// Implement this to initiate the cancellation process.
        /// </summary>
        /// <remarks>
        /// Implementation should:
        /// - Handle cleanup operations
        /// - Notify relevant components
        /// - Manage cancellation state
        /// </remarks>
        void RequestCancellation();

        /// <summary>
        /// Gets whether cancellation has been requested.
        /// Use this to check the cancellation state.
        /// </summary>
        /// <remarks>
        /// Implementation should:
        /// - Be thread-safe
        /// - Return current cancellation state
        /// - Handle race conditions
        /// </remarks>
        bool IsCancellationRequested { get; }

        // TODO: Consider adding these methods:
        // - void RegisterCancellationCallback(Action callback)
        // - Task WaitForCancellationComplete()
        // - void Reset()
    }
}
