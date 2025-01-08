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
 * Defines the contract for error handling operations.
 * This service manages error detection, logging, and recovery
 * strategies across component operations.
 */

using System;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Interface for error handling. Implement this to provide
    /// comprehensive error management in your components.
    /// </summary>
    public interface IErrorHandler
    {
        /// <summary>
        /// Handles exceptions that occur during component execution.
        /// Implement this to process and manage exceptions.
        /// </summary>
        /// <param name="ex">The exception to handle</param>
        /// <remarks>
        /// Implementation should:
        /// - Log the error appropriately
        /// - Update component state
        /// - Notify relevant services
        /// - Consider recovery strategies
        /// </remarks>
        void HandleError(Exception ex);

        /// <summary>
        /// Logs error messages without throwing exceptions.
        /// Implement this for non-critical errors.
        /// </summary>
        /// <param name="message">The error message to log</param>
        /// <remarks>
        /// Implementation should:
        /// - Format message consistently
        /// - Consider error severity
        /// - Handle logging configuration
        /// - Manage log storage
        /// </remarks>
        void LogError(string message);

        /// <summary>
        /// Gets whether any errors have occurred.
        /// Use this to check the error state.
        /// </summary>
        /// <remarks>
        /// Implementation should:
        /// - Be thread-safe
        /// - Consider error persistence
        /// - Handle error state reset
        /// </remarks>
        bool HasErrors { get; }

        // TODO: Consider adding these methods:
        // - void ClearErrors()
        // - IEnumerable<Exception> GetErrors()
        // - ErrorSeverity GetErrorSeverity()
    }
}
