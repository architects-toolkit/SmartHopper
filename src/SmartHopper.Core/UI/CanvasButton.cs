/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * CanvasButton.cs
 * Provides a floating canvas button that triggers a predefined AI chat interface.
 * Auto-initializes when Core is loaded.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Rhino;
using SmartHopper.Core.UI.Chat;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Properties;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Core.UI
{
    /// <summary>
    /// A floating canvas button that triggers a predefined AI chat interface.
    /// Auto-initializes when SmartHopper Core is loaded.
    /// </summary>
    public static class CanvasButton
    {
        // Constants for button appearance and behavior
        private const int ButtonSize = 48;
        private const int ButtonMargin = 20;

        // Predefined system prompt for SmartHopper assistant
        private const string DefaultSystemPrompt = """
            You are a helpful AI assistant specialized in Grasshopper 3D and computational design. Follow these guidelines:

            - Be concise and technical in your responses
            - Explain complex concepts in simple terms
            - Avoid exposing Guids to the user
            - When providing code, include brief comments explaining key parts
            - To know about the user's latest edits in canvas, use gh_get regularly
            - If a question is unclear, ask for clarification
            - Admit when you don't know something rather than guessing
            - Respect the user's skill level and adjust explanations accordingly

            Focus on:
            1. Parametric design principles
            2. Algorithmic problem-solving
            3. Performance optimization
            4. Best practices in computational design

            Examples of tool calls:
            - gh_get: read the current canvas to know about the user's current structure of components
              - gh_get[attrFilters="selected"]: get only selected components
              - gh_get[attrFilters="selected +error"]: get only selected components with errors
              - gh_get[attrFilters="+error +warning"]: get all components with errors or warnings
              - gh_get[guidFilter="guid1"]: get all info about a specific component by its GUID
            - gh_list_components: list installed components to know about the user's available tools
            - gh_group: group components to highlight them to the user, or make notes about them
            - web_rhino_forum_search: look up Rhino forum discussions to try to find answers to the user's question
            - web_rhino_forum_read_post: read a specific post from the Rhino forum
            - web_generic_page_read: read a web page by providing the URL
            """;

        // Private fields
        private static readonly object LockObject = new object();
        private static bool isInitialized;
        private static Bitmap? buttonIcon;

        // Button fields
        private static RectangleF buttonBounds; // Window/client coordinates for both drawing and interaction
        private static bool isHovering;
        private static bool isPressed;

        // Track hooked canvases to allow proper unhooking
        private static readonly HashSet<GH_Canvas> hookedCanvases = new HashSet<GH_Canvas>();

        // Stable dialog ID so the canvas button always reuses the same chat dialog
        private static readonly Guid CanvasChatDialogId = new Guid("B0D0B0F1-1A2B-4C5D-9E0F-112233445566");

        /// <summary>
        /// Initializes static members of the <see cref="CanvasButton"/> class.
        /// Static constructor to auto-initialize when first accessed.
        /// </summary>
        static CanvasButton()
        {
            // Start auto-initialization on a background thread to avoid blocking
            Task.Run(async () => await AutoInitializeAsync().ConfigureAwait(false));
        }

        /// <summary>
        /// Public method to ensure the canvas button is initialized.
        /// This triggers the static constructor if not already called.
        /// </summary>
        public static void EnsureInitialized()
        {
            // This method body is intentionally empty.
            // Just accessing this static class will trigger the static constructor
            // which starts the auto-initialization process.
        }

        /// <summary>
        /// Manually disposes the canvas button system (if needed for cleanup).
        /// </summary>
        public static void Dispose()
        {
            lock (LockObject)
            {
                if (!isInitialized)
                {
                    return;
                }

                try
                {
                    // Unhook all canvas events
                    UnhookAllCanvasEvents();

                    // Unsubscribe from events
                    Instances.CanvasCreated -= OnCanvasCreated;

                    // Dispose resources
                    buttonIcon?.Dispose();
                    buttonIcon = null;

                    isInitialized = false;
                    Debug.WriteLine("[CanvasButton] Canvas button system disposed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CanvasButton] Error disposing canvas button: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Auto-initializes the canvas button system when Grasshopper is ready.
        /// </summary>
        private static async Task AutoInitializeAsync()
        {
            try
            {
                Debug.WriteLine("[CanvasButton] Auto-initialization triggered");

                // Wait for Grasshopper to be available
                await WaitForGrasshopperAsync().ConfigureAwait(false);

                // Initialize the button system only if enabled by settings
                if (IsEnabled())
                {
                    // Ensure initialization happens on the UI thread
                    RhinoApp.InvokeOnUiThread(() => Initialize());
                }
                else
                {
                    Debug.WriteLine("[CanvasButton] Skipping initialization because EnableCanvasButton is disabled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasButton] Auto-initialization error: {ex.Message}");
            }
        }

        /// <summary>
        /// Waits for Grasshopper to be available and ready.
        /// </summary>
        private static async Task WaitForGrasshopperAsync()
        {
            const int maxWaitSeconds = 30;
            const int checkIntervalMs = 500;
            int totalWaited = 0;

            while (totalWaited < maxWaitSeconds * 1000)
            {
                try
                {
                    // Check if Grasshopper is available
                    if (Instances.ActiveCanvas != null || Instances.DocumentServer != null)
                    {
                        Debug.WriteLine("[CanvasButton] Grasshopper is available, proceeding with initialization");
                        return;
                    }
                }
                catch
                {
                    // Grasshopper not ready yet
                }

                await Task.Delay(checkIntervalMs).ConfigureAwait(false);
                totalWaited += checkIntervalMs;
            }

            Debug.WriteLine("[CanvasButton] Timeout waiting for Grasshopper, proceeding anyway");
        }

        /// <summary>
        /// Initializes the canvas button system.
        /// </summary>
        private static void Initialize()
        {
            lock (LockObject)
            {
                if (isInitialized)
                {
                    return;
                }

                try
                {
                    // Load the button icon
                    LoadButtonIcon();

                    // Subscribe to canvas events
                    Instances.CanvasCreated += OnCanvasCreated;

                    // If there's already an active canvas, hook into it
                    if (Instances.ActiveCanvas != null)
                    {
                        HookCanvasEvents(Instances.ActiveCanvas);
                    }

                    isInitialized = true;
                    Debug.WriteLine("[CanvasButton] Canvas button system initialized (auto-init)");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CanvasButton] Error initializing canvas button: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Loads the button icon from embedded resources.
        /// </summary>
        private static void LoadButtonIcon()
        {
            try
            {
                using (var ms = new MemoryStream(providersResources.smarthopper_256))
                {
                    buttonIcon = new Bitmap(ms);
                    Debug.WriteLine("[CanvasButton] Button icon loaded successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasButton] Error loading button icon: {ex.Message}");
                buttonIcon = CreateFallbackIcon();
            }
        }

        /// <summary>
        /// Creates a simple fallback icon when the main icon can't be loaded.
        /// </summary>
        private static Bitmap CreateFallbackIcon()
        {
            var bitmap = new Bitmap(ButtonSize, ButtonSize);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.FillEllipse(new SolidBrush(Color.FromArgb(100, 70, 130, 180)), 4, 4, ButtonSize - 8, ButtonSize - 8);
                g.DrawEllipse(new Pen(Color.FromArgb(150, 70, 130, 180), 2), 4, 4, ButtonSize - 8, ButtonSize - 8);

                // Draw "Chat" text
                using (var font = new Font("Arial", 12, FontStyle.Bold))
                {
                    var textBrush = new SolidBrush(Color.White);
                    var textSize = g.MeasureString("Chat", font);
                    var x = (ButtonSize - textSize.Width) / 2;
                    var y = (ButtonSize - textSize.Height) / 2;
                    g.DrawString("Chat", font, textBrush, x, y);
                }
            }

            return bitmap;
        }

        /// <summary>
        /// Called when a new canvas is created.
        /// </summary>
        private static void OnCanvasCreated(GH_Canvas canvas)
        {
            if (!IsEnabled())
            {
                return;
            }

            HookCanvasEvents(canvas);
        }

        /// <summary>
        /// Hooks into canvas events for the specified canvas viewport.
        /// </summary>
        private static void HookCanvasEvents(GH_Canvas canvas)
        {
            try
            {
                if (hookedCanvases.Contains(canvas))
                {
                    return; // Already hooked
                }

                canvas.CanvasPostPaintOverlay += OnCanvasPostPaintOverlay;
                canvas.MouseDown += OnCanvasMouseDown;
                canvas.MouseUp += OnCanvasMouseUp;
                canvas.MouseMove += OnCanvasMouseMove;

                hookedCanvases.Add(canvas);
                Debug.WriteLine("[CanvasButton] Canvas events hooked successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasButton] Error hooking canvas events: {ex.Message}");
            }
        }

        /// <summary>
        /// Unhooks canvas events for the specified canvas.
        /// </summary>
        private static void UnhookCanvasEvents(GH_Canvas canvas)
        {
            try
            {
                canvas.CanvasPostPaintOverlay -= OnCanvasPostPaintOverlay;
                canvas.MouseDown -= OnCanvasMouseDown;
                canvas.MouseUp -= OnCanvasMouseUp;
                canvas.MouseMove -= OnCanvasMouseMove;

                hookedCanvases.Remove(canvas);
                Debug.WriteLine("[CanvasButton] Canvas events unhooked successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasButton] Error unhooking canvas events: {ex.Message}");
            }
        }

        /// <summary>
        /// Unhooks canvas events for all currently hooked canvases.
        /// </summary>
        private static void UnhookAllCanvasEvents()
        {
            try
            {
                // Create a snapshot to avoid modifying collection during iteration
                var canvases = new List<GH_Canvas>(hookedCanvases);
                foreach (var c in canvases)
                {
                    UnhookCanvasEvents(c);
                }

                hookedCanvases.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasButton] Error unhooking all canvas events: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after the canvas overlay is painted.
        /// </summary>
        private static void OnCanvasPostPaintOverlay(GH_Canvas canvas)
        {
            try
            {
                if (!IsEnabled())
                {
                    // Ensure no hover/press state is kept when disabled
                    isHovering = false;
                    isPressed = false;
                    return;
                }
                // Update button position based on current canvas and window
                UpdateButtonPosition(canvas);
                DrawButton(canvas.Graphics);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasButton] Error drawing button: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the button position to the top-right corner of the viewport.
        /// </summary>
        private static void UpdateButtonPosition(GH_Canvas canvas)
        {
            // Position button in top-right corner using window/client coordinates
            // This ensures the button stays fixed in the viewport regardless of canvas panning
            var x = canvas.ClientSize.Width - ButtonSize - ButtonMargin;
            var y = ButtonMargin;
            buttonBounds = new RectangleF(x, y, ButtonSize, ButtonSize);
        }

        /// <summary>
        /// Draws the button on the canvas.
        /// </summary>
        private static void DrawButton(Graphics graphics)
        {
            if (buttonIcon == null)
            {
                return;
            }

            // Save the current graphics transform and reset to identity
            // This ensures the button renders in window coordinates, not canvas coordinates
            var savedTransform = graphics.Transform;
            graphics.ResetTransform();

            try
            {
                // Create button appearance based on state
                var alpha = isPressed ? 200 : (isHovering ? 255 : 180);
                var scale = isPressed ? 0.95f : (isHovering ? 1.05f : 1.0f);

                // Calculate scaled bounds using window coordinates
                var scaledSize = ButtonSize * scale;
                var offset = (ButtonSize - scaledSize) / 2;
                var scaledBounds = new RectangleF(
                    buttonBounds.X + offset,
                    buttonBounds.Y + offset,
                    scaledSize,
                    scaledSize);

                // Draw shadow
                if (!isPressed)
                {
                    var shadowBounds = new RectangleF(
                        scaledBounds.X + 2,
                        scaledBounds.Y + 2,
                        scaledBounds.Width,
                        scaledBounds.Height);

                    using (var shadowBrush = new SolidBrush(Color.FromArgb(50, Color.Black)))
                    {
                        graphics.FillEllipse(shadowBrush, shadowBounds);
                    }
                }

                // Draw button background
                using (var bgBrush = new SolidBrush(Color.FromArgb(alpha, Color.White)))
                {
                    graphics.FillEllipse(bgBrush, scaledBounds);
                }

                // Draw button border
                using (var borderPen = new Pen(Color.FromArgb(alpha, Color.Gray), 1))
                {
                    graphics.DrawEllipse(borderPen, scaledBounds);
                }

                // Draw icon with transparency
                var iconAttribs = new System.Drawing.Imaging.ImageAttributes();
                var colorMatrix = new System.Drawing.Imaging.ColorMatrix();
                colorMatrix.Matrix33 = alpha / 255f; // Set alpha
                iconAttribs.SetColorMatrix(colorMatrix);

                // Calculate icon bounds (smaller than button to provide padding)
                var iconPadding = scaledSize * 0.15f;
                var iconBounds = new RectangleF(
                    scaledBounds.X + iconPadding,
                    scaledBounds.Y + iconPadding,
                    scaledBounds.Width - (iconPadding * 2),
                    scaledBounds.Height - (iconPadding * 2));

                graphics.DrawImage(
                    buttonIcon,
                    Rectangle.Round(iconBounds),
                    0,
                    0,
                    buttonIcon.Width,
                    buttonIcon.Height,
                    GraphicsUnit.Pixel,
                    iconAttribs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasButton] Error drawing button: {ex.Message}");
            }
            finally
            {
                // Restore the original graphics transform
                graphics.Transform = savedTransform;
            }
        }

        /// <summary>
        /// Handles mouse down events on the canvas viewport.
        /// </summary>
        private static void OnCanvasMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!IsEnabled())
            {
                return;
            }

            if (IsPointInButton(new PointF(e.X, e.Y)))
            {
                isPressed = true;
                (sender as GH_Canvas)?.Refresh();
            }
        }

        /// <summary>
        /// Handles mouse up events on the canvas viewport.
        /// </summary>
        private static void OnCanvasMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!IsEnabled())
            {
                return;
            }

            if (isPressed && IsPointInButton(new PointF(e.X, e.Y)))
            {
                isPressed = false;

                // Trigger the chat dialog
                Task.Run(async () => await TriggerChatDialog().ConfigureAwait(false));

                (sender as GH_Canvas)?.Refresh();
            }
            else if (isPressed)
            {
                isPressed = false;
                (sender as GH_Canvas)?.Refresh();
            }
        }

        /// <summary>
        /// Handles mouse move events on the canvas viewport.
        /// </summary>
        private static void OnCanvasMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!IsEnabled())
            {
                return;
            }

            var wasHovering = isHovering;
            isHovering = IsPointInButton(new PointF(e.X, e.Y));

            if (wasHovering != isHovering)
            {
                (sender as GH_Canvas)?.Refresh();
            }
        }

        /// <summary>
        /// Checks if a point is within the button bounds.
        /// </summary>
        private static bool IsPointInButton(PointF point)
        {
            if (!IsEnabled())
            {
                return false;
            }

            // Check if point is within circular button bounds
            var center = new PointF(
                buttonBounds.X + (buttonBounds.Width / 2),
                buttonBounds.Y + (buttonBounds.Height / 2));

            var distance = Math.Sqrt(Math.Pow(point.X - center.X, 2) + Math.Pow(point.Y - center.Y, 2));
            return distance <= ButtonSize / 2;
        }

        /// <summary>
        /// Returns whether the SmartHopper Assistant canvas button is enabled in settings.
        /// Defaults to true if settings are unavailable.
        /// </summary>
        private static bool IsEnabled()
        {
            try
            {
                return SmartHopperSettings.Instance?.SmartHopperAssistant?.EnableCanvasButton ?? true;
            }
            catch
            {
                // Be permissive if settings are not yet initialized
                return true;
            }
        }

        /// <summary>
        /// Applies the current EnableCanvasButton setting by initializing or disposing the canvas button system.
        /// Safe to call multiple times; does nothing if state already matches.
        /// </summary>
        public static void UpdateEnabledStateFromSettings()
        {
            try
            {
                RhinoApp.InvokeOnUiThread(() =>
                {
                    lock (LockObject)
                    {
                        var shouldEnable = IsEnabled();
                        if (shouldEnable && !isInitialized)
                        {
                            Initialize();
                        }
                        else if (!shouldEnable && isInitialized)
                        {
                            Dispose();
                        }

                        // Refresh active canvas to reflect UI change immediately
                        try
                        {
                            Instances.ActiveCanvas?.Refresh();
                        }
                        catch
                        {
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasButton] Error updating enabled state: {ex.Message}");
            }
        }

        /// <summary>
        /// Triggers the AI chat dialog with the predefined system prompt.
        /// </summary>
        private static async Task TriggerChatDialog()
        {
            try
            {
                Debug.WriteLine("[CanvasButton] Triggering AI chat dialog");

                // Resolve provider and model from SmartHopperAssistant settings with fallbacks
                var providerManager = ProviderManager.Instance;
                var assistant = SmartHopperSettings.Instance?.SmartHopperAssistant;

                var requestedProviderName = assistant?.AssistantProvider;
                // Treat "(Default)" as request to use configured default provider
                var isDefaultProviderSelected = string.Equals(requestedProviderName, "(Default)", StringComparison.Ordinal);
                var providerNameToUse = (!string.IsNullOrWhiteSpace(requestedProviderName) && !isDefaultProviderSelected)
                    ? requestedProviderName
                    : providerManager.GetDefaultAIProvider();

                var provider = providerManager.GetProvider(providerNameToUse);

                // If a requested provider is not available, fall back to the default provider
                if (provider == null)
                {
                    var fallbackProviderName = providerManager.GetDefaultAIProvider();
                    if (!string.Equals(fallbackProviderName, providerNameToUse, StringComparison.OrdinalIgnoreCase))
                    {
                        provider = providerManager.GetProvider(fallbackProviderName);
                    }
                }

                if (provider == null)
                {
                    Debug.WriteLine("[CanvasButton] No AI provider available");
                    StyledMessageDialog.ShowError("No available AI provider was found. Please check the SmartHopper settings to ensure that at least one AI provider is both configured and enabled.", "SmartHopper Assistant");
                    return;
                }

                var providerName = provider.Name;
                var requestedModel = assistant?.AssistantModel;
                // If user selected (Default) provider, always use provider default model
                var model = isDefaultProviderSelected
                    ? provider.GetDefaultModel(AICapability.Text2Text)
                    : (!string.IsNullOrWhiteSpace(requestedModel)
                        ? requestedModel
                        : provider.GetDefaultModel(AICapability.Text2Text));

                Debug.WriteLine($"[CanvasButton] Using provider: {providerName}, model: {model}");

                // Create and process the web chat worker
                var chatWorker = WebChatUtils.CreateWebChatWorker(
                    providerName,
                    model,
                    endpoint: "canvas-chat",
                    systemPrompt: DefaultSystemPrompt,
                    toolFilter: "Components,ComponentsRetrieval,Knowledge,Scripting",
                    componentId: CanvasChatDialogId);

                await chatWorker.ProcessChatAsync(default).ConfigureAwait(false);

                Debug.WriteLine("[CanvasButton] Chat dialog completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CanvasButton] Error triggering chat dialog: {ex.Message}");
            }
        }
    }
}
