using System.Collections.Generic;

namespace SmartHopper.Core.AI
{
    /// <summary>
    /// Static manager for handling AI context providers
    /// </summary>
    public static class AIContextManager
    {
        private static IAIContextProvider _contextProvider;

        /// <summary>
        /// Sets the context provider for AI queries
        /// </summary>
        /// <param name="provider">The context provider implementation</param>
        public static void SetContextProvider(IAIContextProvider provider)
        {
            _contextProvider = provider;
        }

        /// <summary>
        /// Gets the current context from the registered provider
        /// </summary>
        /// <returns>A dictionary of context key-value pairs, or empty if no provider is registered</returns>
        public static Dictionary<string, string> GetCurrentContext()
        {
            return _contextProvider?.GetContext() ?? new Dictionary<string, string>();
        }
    }
}
