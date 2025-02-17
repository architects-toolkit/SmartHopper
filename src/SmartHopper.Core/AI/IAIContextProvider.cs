using System.Collections.Generic;

namespace SmartHopper.Core.AI
{
    /// <summary>
    /// Interface for providing context to AI queries
    /// </summary>
    public interface IAIContextProvider
    {
        /// <summary>
        /// Gets the current context for AI queries
        /// </summary>
        /// <returns>A dictionary of context key-value pairs</returns>
        Dictionary<string, string> GetContext();
    }
}
