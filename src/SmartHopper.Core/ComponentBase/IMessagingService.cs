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
 * Defines the contract for component messaging and progress reporting.
 * This service handles all user-facing messages, ensuring proper thread
 * synchronization for UI updates and consistent message formatting.
 */

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Interface for component messaging services. Implement this to handle
    /// all forms of user communication from your components.
    /// </summary>
    public interface IMessagingService
    {
        /// <summary>
        /// Reports progress during component execution.
        /// Implement this to show operation progress to users.
        /// </summary>
        /// <param name="message">The progress message to display</param>
        /// <remarks>
        /// Implementation should:
        /// - Ensure thread safety for UI updates
        /// - Format messages consistently
        /// - Handle message queuing if needed
        /// </remarks>
        void ReportProgress(string message);

        /// <summary>
        /// Reports errors during component execution.
        /// Implement this to show error messages to users.
        /// </summary>
        /// <param name="error">The error message to display</param>
        /// <remarks>
        /// Implementation should:
        /// - Format error messages consistently
        /// - Log errors appropriately
        /// - Consider error severity levels
        /// </remarks>
        void ReportError(string error);

        /// <summary>
        /// Updates the component's status message.
        /// Implement this to show the current component state.
        /// </summary>
        /// <param name="message">The status message to display</param>
        /// <remarks>
        /// Implementation should:
        /// - Handle UI thread synchronization
        /// - Manage message persistence
        /// - Consider message priority
        /// </remarks>
        void UpdateComponentMessage(string message);

        // TODO: Consider adding these methods:
        // - void SetMessagePriority(MessagePriority priority)
        // - void ClearMessages()
        // - IEnumerable<string> GetMessageHistory()
    }
}
