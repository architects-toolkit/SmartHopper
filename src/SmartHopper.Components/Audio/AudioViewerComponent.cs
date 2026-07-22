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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.Types;

namespace SmartHopper.Components.Audio
{
    /// <summary>
    /// Grasshopper component for viewing, playing, and saving audio files.
    /// </summary>
    public class AudioViewerComponent : GH_Component
    {
        private byte[] _audioData;
        private readonly object _audioLock = new object();
        private string _currentAudioPath = string.Empty;
        private VersatileAudio _currentAudio;

        // Execution throttling fields
        private DateTime _lastSaveTime = DateTime.MinValue;
        private string _lastSavedPath = string.Empty;
        private readonly TimeSpan _minSaveInterval = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("26080D3E-05B4-4729-8E45-3EAA7C3AA864");

        /// <summary>
        /// Gets the icon for this component.
        /// </summary>
        protected override Bitmap Icon => null;

        /// <summary>
        /// Gets the exposure level of this component in the ribbon.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the AudioViewerComponent class.
        /// </summary>
        public AudioViewerComponent()
            : base(
                "Audio Viewer",
                "AudioView",
                "Display audio files on the canvas, play them, and save them to disk.",
                "SmartHopper",
                "Audio")
        {
        }

        /// <summary>
        /// Creates the custom attributes for this component.
        /// </summary>
        public override void CreateAttributes()
        {
            this.m_attributes = new AudioViewerAttributes(this);
        }

        /// <summary>
        /// Registers input parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register inputs with.</param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Audio", "A", "Audio file to display and play", GH_ParamAccess.item);
            pManager.AddTextParameter("File Path", "F", "File path to save the audio.\nAdd one of the compatible extensions to the file name: .mp3, .wav, .m4a, .aac, .flac, .ogg, .wma, .opus", GH_ParamAccess.item, string.Empty);
            pManager.AddBooleanParameter("Play?", "P", "Trigger to play the audio", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Save?", "S", "Trigger to save the audio to the specified file path", GH_ParamAccess.item, false);

            // Make parameters optional
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register outputs with.</param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// Main solving method for the component.
        /// </summary>
        /// <param name="DA">Data access object.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get inputs
            IGH_Goo audioGoo = null;
            string filePath = string.Empty;
            bool play = false;
            bool save = false;

            DA.GetData(0, ref audioGoo);
            DA.GetData(1, ref filePath);
            DA.GetData(2, ref play);
            DA.GetData(3, ref save);

            VersatileAudio audio = null;

            // Extract audio from input
            if (audioGoo != null)
            {
                // Try to get audio from GH_VersatileAudio
                if (audioGoo is GH_VersatileAudio versatileAudio && versatileAudio.Value != null)
                {
                    audio = versatileAudio.Value;
                }

                // Try to get audio from string
                else if (audioGoo.ScriptVariable() is string audioPath)
                {
                    try
                    {
                        audio = VersatileAudio.FromString(audioPath);
                    }
                    catch (Exception ex)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Failed to parse audio path: {ex.Message}");
                    }
                }
            }

            if (audio != null)
            {
                // Store audio data
                this.SetAudioData(audio);

                // Handle playback if requested
                if (play)
                {
                    this.PlayAudio(audio);
                }

                // Handle saving if requested and path is provided
                if (save && !string.IsNullOrWhiteSpace(filePath))
                {
                    this.SaveAudio(audio, filePath);
                }
                else if (save && string.IsNullOrWhiteSpace(filePath))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Play/Save triggered but no file path provided");
                }
            }
            else if (audioGoo != null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input is not a valid audio file");
                this.SetAudioData(null);
            }
            else
            {
                this.SetAudioData(null);
            }
        }

        /// <summary>
        /// Plays the audio file using the system default audio player.
        /// </summary>
        /// <param name="audio">The audio to play.</param>
        private void PlayAudio(VersatileAudio audio)
        {
            if (audio == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No audio to play");
                return;
            }

            try
            {
                string audioPath = null;

                // Get the file path based on audio kind
                switch (audio.Kind)
                {
                    case VersatileAudioKind.LocalFile:
                        audioPath = audio.RawValue;
                        if (!File.Exists(audioPath))
                        {
                            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Audio file not found: {audioPath}");
                            return;
                        }

                        break;

                    case VersatileAudioKind.Url:
                        // For URLs, we'll open them directly with the system default player
                        audioPath = audio.RawValue;
                        break;

                    case VersatileAudioKind.Base64:
                    case VersatileAudioKind.DataUri:
                        // For base64/data-uri, save to temp file and play
                        var tempFile = Path.Combine(Path.GetTempPath(), $"smarthopper_audio_{Guid.NewGuid():N}.mp3");
                        try
                        {
                            var audioBytes = audio.ToByteArray();
                            File.WriteAllBytes(tempFile, audioBytes);
                            audioPath = tempFile;
                        }
                        catch (Exception ex)
                        {
                            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to extract audio data: {ex.Message}");
                            return;
                        }

                        break;
                }

                if (!string.IsNullOrEmpty(audioPath))
                {
                    // Use system default player
                    var psi = new ProcessStartInfo
                    {
                        FileName = audioPath,
                        UseShellExecute = true,
                    };

                    Process.Start(psi);
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Playing audio: {Path.GetFileName(audioPath)}");
                }
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to play audio: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the audio to the specified file path.
        /// </summary>
        /// <param name="audio">The audio to save.</param>
        /// <param name="filePath">The target file path.</param>
        private void SaveAudio(VersatileAudio audio, string filePath)
        {
            if (audio == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No audio to save");
                return;
            }

            // Execution throttling: prevent rapid successive saves
            var currentTime = DateTime.Now;
            var timeSinceLastSave = currentTime - this._lastSaveTime;
            var isSameFile = string.Equals(filePath, this._lastSavedPath, StringComparison.OrdinalIgnoreCase);

            if (isSameFile && timeSinceLastSave < this._minSaveInterval)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Save throttled. Wait {(this._minSaveInterval - timeSinceLastSave).TotalMilliseconds:F0}ms before next save.");
                return;
            }

            // Ensure directory exists with race condition handling
            var directory = Path.GetDirectoryName(filePath);
            if (!EnsureDirectoryExists(directory))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to create directory: {directory}");
                return;
            }

            // Check if target file is locked
            if (IsFileLocked(filePath))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"File is currently locked by another process: {filePath}");
                return;
            }

            // Use temp file strategy for safe saving
            var tempFilePath = GetTempFilePath(filePath);

            try
            {
                // Get audio bytes
                byte[] audioBytes;
                try
                {
                    audioBytes = audio.ToByteArray();
                }
                catch (Exception ex)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to extract audio data: {ex.Message}");
                    return;
                }

                // Save to temp file first
                File.WriteAllBytes(tempFilePath, audioBytes);

                // Move temp file to final location (atomic operation)
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                File.Move(tempFilePath, filePath);

                // Update throttling state
                this._lastSaveTime = currentTime;
                this._lastSavedPath = filePath;

                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Audio saved successfully to: {filePath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                this.CleanupTempFile(tempFilePath);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Access denied saving audio: {ex.Message}. Check file permissions.");
            }
            catch (DirectoryNotFoundException ex)
            {
                this.CleanupTempFile(tempFilePath);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Directory not found: {ex.Message}");
            }
            catch (IOException ex)
            {
                this.CleanupTempFile(tempFilePath);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"I/O error saving audio: {ex.Message}. File may be in use.");
            }
            catch (Exception ex)
            {
                this.CleanupTempFile(tempFilePath);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unexpected error saving audio: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a file is currently locked by another process.
        /// </summary>
        /// <param name="filePath">Path to the file to check.</param>
        /// <returns>True if file is locked, false otherwise.</returns>
        private static bool IsFileLocked(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                using (var stream = File.Open(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        /// <summary>
        /// Generates a safe temporary file path in the same directory as the target file.
        /// </summary>
        /// <param name="targetPath">The final target file path.</param>
        /// <returns>A temporary file path.</returns>
        private static string GetTempFilePath(string targetPath)
        {
            var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(targetPath);
            var extension = Path.GetExtension(targetPath);
            var randomPart = Path.GetRandomFileName().Replace(".", string.Empty);
            var tempFileName = $"{fileName}_temp_{randomPart}{extension}";
            return Path.Combine(directory, tempFileName);
        }

        /// <summary>
        /// Safely creates directory with proper error handling for race conditions.
        /// </summary>
        /// <param name="directoryPath">Directory path to create.</param>
        /// <returns>True if directory exists or was created successfully.</returns>
        private static bool EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return true;

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                return true;
            }
            catch (IOException)
            {
                return Directory.Exists(directoryPath);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Safely cleans up a temporary file if it exists.
        /// </summary>
        /// <param name="tempFilePath">Path to the temporary file to clean up.</param>
        private void CleanupTempFile(string tempFilePath)
        {
            if (string.IsNullOrEmpty(tempFilePath))
            {
                return;
            }

            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Failed to cleanup temp file {tempFilePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the audio data for display.
        /// </summary>
        /// <param name="audio">The audio to display.</param>
        internal void SetAudioData(VersatileAudio audio)
        {
            lock (this._audioLock)
            {
                this._currentAudio = audio;
                if (audio != null)
                {
                    try
                    {
                        this._audioData = audio.ToByteArray();
                        this._currentAudioPath = audio.RawValue;
                    }
                    catch
                    {
                        this._audioData = null;
                        this._currentAudioPath = string.Empty;
                    }
                }
                else
                {
                    this._audioData = null;
                    this._currentAudioPath = string.Empty;
                }
            }

            // Update the display
            this.OnDisplayExpired(false);
        }

        /// <summary>
        /// Gets the current audio data.
        /// </summary>
        /// <returns>The audio data, or null if none.</returns>
        public byte[] GetAudioData()
        {
            lock (this._audioLock)
            {
                return this._audioData?.Length > 0 ? (byte[])this._audioData.Clone() : null;
            }
        }

        /// <summary>
        /// Gets the current audio path or URL.
        /// </summary>
        /// <returns>The audio path/URL, or empty string if none.</returns>
        public string GetAudioPath()
        {
            lock (this._audioLock)
            {
                return this._currentAudioPath ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the current audio object.
        /// </summary>
        /// <returns>The VersatileAudio object, or null if none.</returns>
        public VersatileAudio GetAudio()
        {
            lock (this._audioLock)
            {
                return this._currentAudio;
            }
        }
    }
}
