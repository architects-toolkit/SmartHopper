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
using Grasshopper.Kernel;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Wraps audio file input into an AIInputPayload for AI audio processing.
    /// Supports common audio formats (mp3, wav, m4a, ogg, flac).
    /// </summary>
    public class Audio2AIComponent : AIInputAdapterBase
    {
        public override Guid ComponentGuid => new Guid("24407233-D8B8-4812-9F5F-30C39CFC7459");

        protected override Bitmap Icon => Resources.audio2ai;

        public Audio2AIComponent()
            : base(
                  "Audio to AI",
                  "Audio2AI",
                  "Wraps audio file input into an AIInputPayload for AI audio processing.",
                  GH_Exposure.secondary)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Path to the audio file to wrap into an AIInputPayload.", GH_ParamAccess.item);
        }

        protected override string PayloadOutputDescription => "AIInputPayload wrapping the audio file for AI processing.";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filePath = null;
            if (!DA.GetData(0, ref filePath))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File path cannot be empty.");
                return;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Audio file not found: {filePath}");
                    return;
                }

                var mimeType = GetMimeTypeFromPath(filePath);
                if (string.IsNullOrEmpty(mimeType))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unsupported audio format: {Path.GetExtension(filePath)}");
                    return;
                }

                var audioInteraction = new AIInteractionAudio
                {
                    FilePath = filePath,
                    MimeType = mimeType,
                };

                var payload = this.CreateAudioPayload(audioInteraction);
                DA.SetData(0, this.WrapPayload(payload));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error creating audio payload: {ex.Message}");
            }
        }

        private static string GetMimeTypeFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".m4a" => "audio/mp4",
                ".ogg" => "audio/ogg",
                ".flac" => "audio/flac",
                _ => null,
            };
        }
    }
}
