/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Drawing;
using System.IO;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using SmartHopper.Core.Types;

namespace SmartHopper.Components.Audio
{
    /// <summary>
    /// Custom display attributes for the AudioViewerComponent.
    /// Renders a simplified audio timeline visualization with play/save controls.
    /// </summary>
    public class AudioViewerAttributes : GH_ComponentAttributes
    {
        private readonly AudioViewerComponent _component;
        private RectangleF _timelineArea;
        private RectangleF _playButtonArea;
        private RectangleF _saveButtonArea;
        private bool _isPlayHovered;
        private bool _isSaveHovered;

        /// <summary>
        /// Initializes a new instance of the AudioViewerAttributes class.
        /// </summary>
        /// <param name="component">The component to create attributes for.</param>
        public AudioViewerAttributes(AudioViewerComponent component)
            : base(component)
        {
            this._component = component;
        }

        /// <summary>
        /// Layout the component bounds.
        /// </summary>
        protected override void Layout()
        {
            // Let Grasshopper compute the natural bounds based on params, names, etc.
            base.Layout();
            var baseBounds = this.Bounds;

            // Expand bounds to accommodate timeline and controls
            this.Bounds = new RectangleF(
                baseBounds.X,
                baseBounds.Y,
                baseBounds.Width,
                baseBounds.Height + 120);
        }

        /// <summary>
        /// Renders the component on the canvas.
        /// </summary>
        /// <param name="canvas">The canvas to render on.</param>
        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // Always delegate to base for proper channel handling (wires, selection, overlay, etc.)
            base.Render(canvas, graphics, channel);

            if (channel == GH_CanvasChannel.Objects)
            {
                // Get component bounds
                var bounds = this.Bounds;
                var displayArea = new RectangleF(
                    bounds.X + 5,
                    bounds.Bottom - 115,
                    bounds.Width - 10,
                    110);

                // Render timeline area
                this._timelineArea = new RectangleF(
                    displayArea.X,
                    displayArea.Y,
                    displayArea.Width,
                    60);

                this.RenderTimeline(graphics, this._timelineArea);

                // Render control buttons
                var buttonHeight = 25;
                var buttonY = this._timelineArea.Bottom + 5;
                var buttonWidth = (displayArea.Width - 10) / 2;

                this._playButtonArea = new RectangleF(
                    displayArea.X,
                    buttonY,
                    buttonWidth,
                    buttonHeight);

                this._saveButtonArea = new RectangleF(
                    this._playButtonArea.Right + 5,
                    buttonY,
                    buttonWidth,
                    buttonHeight);

                this.RenderButton(graphics, this._playButtonArea, "▶ Play", this._isPlayHovered);
                this.RenderButton(graphics, this._saveButtonArea, "💾 Save", this._isSaveHovered);
            }
        }

        /// <summary>
        /// Renders a simplified audio timeline visualization.
        /// </summary>
        private void RenderTimeline(Graphics graphics, RectangleF area)
        {
            // Background
            using (var brush = new SolidBrush(Color.FromArgb(240, 240, 240)))
            {
                graphics.FillRectangle(brush, area);
            }

            // Border
            using (var pen = new Pen(Color.FromArgb(150, 150, 150), 1))
            {
                graphics.DrawRectangle(pen, area.X, area.Y, area.Width, area.Height);
            }

            var audio = this._component.GetAudio();
            var audioPath = this._component.GetAudioPath();

            if (audio != null && !string.IsNullOrEmpty(audioPath))
            {
                // Draw waveform visualization (simplified)
                this.DrawWaveform(graphics, area, audio);

                // Draw audio info
                var infoText = $"{audio.Kind}: {Path.GetFileName(audioPath)}";
                using (var font = new Font("Arial", 9))
                using (var brush = new SolidBrush(Color.FromArgb(80, 80, 80)))
                {
                    var textSize = graphics.MeasureString(infoText, font);
                    var textX = area.X + 5;
                    var textY = area.Bottom - textSize.Height - 3;
                    graphics.DrawString(infoText, font, brush, textX, textY);
                }
            }
            else
            {
                // Draw placeholder
                using (var font = new Font("Arial", 10, FontStyle.Italic))
                using (var brush = new SolidBrush(Color.FromArgb(150, 150, 150)))
                {
                    var text = "No audio loaded";
                    var textSize = graphics.MeasureString(text, font);
                    var textX = area.X + (area.Width - textSize.Width) / 2;
                    var textY = area.Y + (area.Height - textSize.Height) / 2;
                    graphics.DrawString(text, font, brush, textX, textY);
                }
            }
        }

        /// <summary>
        /// Draws a simplified waveform visualization.
        /// </summary>
        private void DrawWaveform(Graphics graphics, RectangleF area, VersatileAudio audio)
        {
            try
            {
                var audioData = this._component.GetAudioData();
                if (audioData == null || audioData.Length == 0)
                {
                    return;
                }

                // Simplified waveform: sample every Nth byte and draw bars
                var waveformArea = new RectangleF(area.X + 5, area.Y + 5, area.Width - 10, area.Height - 25);
                var barCount = Math.Min(100, (int)(waveformArea.Width / 2));
                var samplesPerBar = Math.Max(1, audioData.Length / barCount);

                using (var pen = new Pen(Color.FromArgb(0, 120, 215), 1.5f))
                {
                    for (int i = 0; i < barCount; i++)
                    {
                        var sampleIndex = i * samplesPerBar;
                        if (sampleIndex >= audioData.Length)
                            break;

                        // Get sample value (0-255 range)
                        var sampleValue = audioData[sampleIndex];
                        var normalizedValue = sampleValue / 255.0f;

                        // Draw bar centered vertically
                        var barHeight = normalizedValue * waveformArea.Height * 0.8f;
                        var barX = waveformArea.X + (i * waveformArea.Width / barCount);
                        var barY = waveformArea.Y + (waveformArea.Height - barHeight) / 2;

                        graphics.DrawLine(pen, barX, barY, barX, barY + barHeight);
                    }
                }
            }
            catch
            {
                // Silently fail waveform rendering
            }
        }

        /// <summary>
        /// Renders a control button.
        /// </summary>
        private void RenderButton(Graphics graphics, RectangleF area, string text, bool isHovered)
        {
            // Background
            var bgColor = isHovered ? Color.FromArgb(0, 120, 215) : Color.FromArgb(200, 200, 200);
            using (var brush = new SolidBrush(bgColor))
            {
                graphics.FillRectangle(brush, area);
            }

            // Border
            var borderColor = isHovered ? Color.FromArgb(0, 80, 160) : Color.FromArgb(100, 100, 100);
            using (var pen = new Pen(borderColor, 1))
            {
                graphics.DrawRectangle(pen, area.X, area.Y, area.Width, area.Height);
            }

            // Text
            var textColor = isHovered ? Color.White : Color.FromArgb(50, 50, 50);
            using (var font = new Font("Arial", 9, FontStyle.Bold))
            using (var brush = new SolidBrush(textColor))
            {
                var textSize = graphics.MeasureString(text, font);
                var textX = area.X + (area.Width - textSize.Width) / 2;
                var textY = area.Y + (area.Height - textSize.Height) / 2;
                graphics.DrawString(text, font, brush, textX, textY);
            }
        }

        /// <summary>
        /// Handles mouse move events for button hover effects.
        /// </summary>
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var oldPlayHovered = this._isPlayHovered;
            var oldSaveHovered = this._isSaveHovered;

            this._isPlayHovered = this._playButtonArea.Contains(e.CanvasLocation);
            this._isSaveHovered = this._saveButtonArea.Contains(e.CanvasLocation);

            if (oldPlayHovered != this._isPlayHovered || oldSaveHovered != this._isSaveHovered)
            {
                sender.Invalidate();
            }

            return base.RespondToMouseMove(sender, e);
        }

        /// <summary>
        /// Handles mouse click events for button interactions.
        /// </summary>
        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (this._playButtonArea.Contains(e.CanvasLocation))
            {
                // Trigger play action
                var audio = this._component.GetAudio();
                if (audio != null)
                {
                    // We'll use a workaround: set the Play input to true
                    // This requires the component to have proper input handling
                    this._component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Play triggered via UI button");
                }

                return GH_ObjectResponse.Handled;
            }

            if (this._saveButtonArea.Contains(e.CanvasLocation))
            {
                // Trigger save action
                var audio = this._component.GetAudio();
                if (audio != null)
                {
                    this._component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Save triggered via UI button");
                }

                return GH_ObjectResponse.Handled;
            }

            return base.RespondToMouseDown(sender, e);
        }
    }
}
