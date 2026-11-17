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
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using SmartHopper.Core.Types;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Components.Audio
{
    /// <summary>
    /// Custom display attributes for the AudioViewerComponent.
    /// Renders a simplified audio timeline visualization and a native-style Play button.
    /// </summary>
    public class AudioViewerAttributes : GH_ComponentAttributes
    {
        private readonly AudioViewerComponent _component;
        private RectangleF _timelineArea;
        private Rectangle _playButtonArea;
        private bool _isPlayHovered;
        private bool _isPlayClicked;

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
        /// Layout the component bounds, the timeline area and the Play button.
        /// </summary>
        protected override void Layout()
        {
            // Let Grasshopper compute the natural bounds based on params, names, etc.
            base.Layout();
            var bounds = this.Bounds;

            const int margin = 5;
            const int timelineHeight = 60;
            const int buttonHeight = 24;

            this._timelineArea = new RectangleF(
                bounds.X + margin,
                bounds.Bottom + margin,
                bounds.Width - (2 * margin),
                timelineHeight);

            this._playButtonArea = new Rectangle(
                (int)(bounds.X + margin),
                (int)(this._timelineArea.Bottom + margin),
                (int)(bounds.Width - (2 * margin)),
                buttonHeight);

            this.Bounds = new RectangleF(
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height + (3 * margin) + timelineHeight + buttonHeight);
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
                this.RenderTimeline(graphics, this._timelineArea);
                this.RenderPlayButton(graphics);
            }
        }

        /// <summary>
        /// Renders the Play button in the native Grasshopper capsule style,
        /// matching the Select button used by selecting components.
        /// </summary>
        private void RenderPlayButton(Graphics graphics)
        {
            var palette = this._isPlayClicked ? GH_Palette.White : (this._isPlayHovered ? GH_Palette.Grey : GH_Palette.Black);
            var capsule = GH_Capsule.CreateCapsule(this._playButtonArea, palette);
            capsule.Render(graphics, this.Selected, this._component.Locked, false);
            capsule.Dispose();

            var font = GH_FontServer.Standard;
            var text = "Play";
            var size = graphics.MeasureString(text, font);
            var tx = this._playButtonArea.X + ((this._playButtonArea.Width - size.Width) / 2);
            var ty = this._playButtonArea.Y + ((this._playButtonArea.Height - size.Height) / 2);
            graphics.DrawString(text, font, (this._isPlayHovered || this._isPlayClicked) ? Brushes.Black : Brushes.White, new PointF(tx, ty));
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

                // Draw audio info: a friendly label, never the raw audio payload
                // (a DataUri/Base64 raw value would flood the component face).
                var infoText = DescribeAudio(audio, audioPath, this._component.GetAudioData());
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
        /// Builds the compact label shown under the waveform. Describes the
        /// audio source without exposing the raw payload.
        /// </summary>
        private static string DescribeAudio(VersatileAudio audio, string audioPath, byte[] audioData)
        {
            switch (audio.Kind)
            {
                case VersatileAudioKind.LocalFile:
                    return $"Local file: {Path.GetFileName(audioPath)}";
                case VersatileAudioKind.Url:
                    var tail = audioPath.Split('?')[0].TrimEnd('/');
                    var name = tail.Substring(tail.LastIndexOf('/') + 1);
                    return string.IsNullOrEmpty(name) ? "Remote audio (Url)" : $"Url: {name}";
                default:
                    var size = audioData?.Length > 0 ? $" — {audioData.Length / 1024.0:F1} KB" : string.Empty;
                    return $"{audio.Kind} audio{size}";
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
            catch (Exception ex)
            {
                // Rendering-only failure — keep it out of the error stream but traceable.
                Logger.For<AudioViewerAttributes>().Trace($"[AudioViewerAttributes] Waveform rendering failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles mouse move events for button hover effects.
        /// </summary>
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var wasHovered = this._isPlayHovered;
            this._isPlayHovered = this._playButtonArea.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y);

            if (wasHovered != this._isPlayHovered)
            {
                sender.Invalidate();
            }

            return base.RespondToMouseMove(sender, e);
        }

        /// <summary>
        /// Handles mouse click events for the Play button.
        /// </summary>
        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && this._playButtonArea.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y))
            {
                this._isPlayClicked = true;
                this._component.PlayCurrentAudio();
                this._component.OnDisplayExpired(false);
                return GH_ObjectResponse.Handled;
            }

            return base.RespondToMouseDown(sender, e);
        }

        /// <summary>
        /// Clears the pressed state so the button repaints in its idle palette.
        /// </summary>
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (this._isPlayClicked)
            {
                this._isPlayClicked = false;
                this._component.OnDisplayExpired(false);
            }

            return base.RespondToMouseUp(sender, e);
        }
    }
}
