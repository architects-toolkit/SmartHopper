using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Dialogs;

namespace SmartHopper.Config.Interfaces
{
    /// <summary>
    /// Interface for provider settings UI and validation.
    /// </summary>
    public interface IAIProviderSettings
    {
        Control CreateSettingsControl();

        Dictionary<string, object> GetSettings();

        void LoadSettings(Dictionary<string, object> settings);

        bool ValidateSettings();
    }

    /// <summary>
    /// Base class for provider settings, encapsulating common UI building and persistence logic.
    /// </summary>
    public abstract class AIProviderSettings : IAIProviderSettings
    {
        protected readonly IAIProvider provider;
        protected TextBox apiKeyTextBox;
        protected TextBox modelTextBox;
        protected NumericUpDown maxTokensNumeric;
        protected string decryptedApiKey;

        protected AIProviderSettings(IAIProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public virtual Control CreateSettingsControl()
        {
            var panel = new TableLayoutPanel
            {
                RowCount = 3,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                AutoSize = true
            };

            // API Key
            panel.Controls.Add(new Label { Text = "API Key:", Dock = DockStyle.Fill }, 0, 0);
            apiKeyTextBox = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
            panel.Controls.Add(apiKeyTextBox, 1, 0);

            // Model
            panel.Controls.Add(new Label { Text = "Model:", Dock = DockStyle.Fill }, 0, 1);
            modelTextBox = new TextBox { Dock = DockStyle.Fill };
            panel.Controls.Add(modelTextBox, 1, 1);

            // Max Tokens
            panel.Controls.Add(new Label { Text = "Max Tokens:", Dock = DockStyle.Fill }, 0, 2);
            maxTokensNumeric = new NumericUpDown { Minimum = 1, Maximum = 4096, Value = 150, Dock = DockStyle.Fill };
            panel.Controls.Add(maxTokensNumeric, 1, 2);

            // Load persisted settings
            LoadSettings();
            return panel;
        }

        public virtual Dictionary<string, object> GetSettings()
        {
            return new Dictionary<string, object>
            {
                ["ApiKey"] = apiKeyTextBox.Text == "<secret-defined>" ? decryptedApiKey ?? string.Empty : apiKeyTextBox.Text,
                ["Model"] = string.IsNullOrWhiteSpace(modelTextBox.Text) ? provider.DefaultModel : modelTextBox.Text,
                ["MaxTokens"] = (int)maxTokensNumeric.Value
            };
        }

        public virtual void LoadSettings(Dictionary<string, object> settings)
        {
            if (settings == null) return;
            try
            {
                // API Key
                if (settings.ContainsKey("ApiKey"))
                {
                    var raw = settings["ApiKey"];
                    if (raw is string key && !string.IsNullOrEmpty(key))
                    {
                        decryptedApiKey = key;
                        apiKeyTextBox.Text = "<secret-defined>";
                    }
                    else if (raw is bool ok && ok)
                    {
                        apiKeyTextBox.Text = "<secret-defined>";
                    }
                    else
                    {
                        apiKeyTextBox.Text = string.Empty;
                    }
                }
                // Model
                if (settings.ContainsKey("Model"))
                    modelTextBox.Text = settings["Model"]?.ToString();
                else
                    modelTextBox.Text = provider.DefaultModel;
                // Max Tokens
                if (settings.ContainsKey("MaxTokens") && settings["MaxTokens"] is int maxTokens)
                    maxTokensNumeric.Value = maxTokens;
                else if (settings.ContainsKey("MaxTokens") && int.TryParse(settings["MaxTokens"]?.ToString(), out int parsed))
                    maxTokensNumeric.Value = parsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading {provider.Name} provider settings: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                ProviderManager.Instance.RefreshProviders();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading {provider.Name} provider settings: {ex.Message}");
            }
        }

        public virtual bool ValidateSettings()
        {
            var text = apiKeyTextBox.Text;
            if (string.IsNullOrWhiteSpace(text) || (text == "<secret-defined>" && string.IsNullOrEmpty(decryptedApiKey)))
            {
                StyledMessageDialog.ShowError("API Key is required.", "Validation Error");
                return false;
            }
            return true;
        }
    }
}
