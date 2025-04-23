using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Models;
using SmartHopper.Config.Managers;

namespace SmartHopper.Config.Interfaces
{
    public interface IAIProvider
    {
        string Name { get; }

        string DefaultModel { get; }

        /// <summary>
        /// Gets the provider's icon. Should return a 16x16 image suitable for display in the UI.
        /// </summary>
        Image Icon { get; }

        /// <summary>
        /// Gets or sets whether this provider is enabled and should be available for use.
        /// This can be used to disable template or experimental providers.
        /// </summary>
        bool IsEnabled { get; }

        IEnumerable<SettingDescriptor> GetSettingDescriptors();

        bool ValidateSettings(Dictionary<string, object> settings);

        Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false);

        string GetModel(Dictionary<string, object> settings, string requestedModel = "");

        /// <summary>
        /// Injects decrypted settings for this provider (called by ProviderManager).
        /// </summary>
        void InitializeSettings(Dictionary<string, object> settings);
    }

    /// <summary>
    /// Base class for AI providers, encapsulating common logic.
    /// </summary>
    public abstract class AIProvider : IAIProvider
    {
        protected Dictionary<string, object> _injectedSettings;

        public abstract string Name { get; }
        public abstract string DefaultModel { get; }
        public abstract bool IsEnabled { get; }
        public abstract Image Icon { get; }

        public abstract IEnumerable<SettingDescriptor> GetSettingDescriptors();
        public abstract bool ValidateSettings(Dictionary<string, object> settings);
        public abstract Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false);

        /// <summary>
        /// Store decrypted settings for use by derived providers.
        /// </summary>
        public void InitializeSettings(Dictionary<string, object> settings)
        {
            _injectedSettings = settings ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Accessor for injected settings.
        /// </summary>
        protected Dictionary<string, object> Settings => _injectedSettings;

        /// <summary>
        /// Default model resolution logic.
        /// </summary>
        public virtual string GetModel(Dictionary<string, object> settings, string requestedModel = "")
        {
            if (!string.IsNullOrWhiteSpace(requestedModel))
                return requestedModel;
            if (settings != null && settings.ContainsKey("Model") && !string.IsNullOrWhiteSpace(settings["Model"]?.ToString()))
                return settings["Model"].ToString();
            return DefaultModel;
        }

        /// <summary>
        /// Common tool formatting for function definitions.
        /// </summary>
        protected JArray GetFormattedTools()
        {
            try
            {
                AIToolManager.DiscoverTools();
                var tools = AIToolManager.GetTools();
                if (tools.Count == 0)
                {
                    Debug.WriteLine("No tools available.");
                    return null;
                }

                var toolsArray = new JArray();
                foreach (var tool in tools)
                {
                    var toolObject = new JObject
                    {
                        ["type"] = "function",
                        ["function"] = new JObject
                        {
                            ["name"] = tool.Value.Name,
                            ["description"] = tool.Value.Description,
                            ["parameters"] = JObject.Parse(tool.Value.ParametersSchema)
                        }
                    };
                    toolsArray.Add(toolObject);
                }
                return toolsArray;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting tools: {ex.Message}");
                return null;
            }
        }
    }
}
