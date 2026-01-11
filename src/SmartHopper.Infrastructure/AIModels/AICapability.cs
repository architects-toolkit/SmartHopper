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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;

namespace SmartHopper.Infrastructure.AIModels
{
    /// <summary>
    /// Defines the capabilities that an AI model can support.
    /// </summary>
    [Flags]
    public enum AICapability
    {
        /// <summary>
        /// No capabilities. Used as a default or placeholder value.
        /// </summary>
        None = 0,

        // Input capabilities

        /// <summary>
        /// Supports accepting textual input (prompts or text content).
        /// </summary>
        TextInput = 1 << 0,

        /// <summary>
        /// Supports accepting image input (image understanding or vision).
        /// </summary>
        ImageInput = 1 << 1,

        /// <summary>
        /// Supports accepting audio input (speech or other audio signals).
        /// </summary>
        AudioInput = 1 << 2,

        /// <summary>
        /// Supports accepting structured JSON input.
        /// </summary>
        JsonInput = 1 << 3,

        // Output capabilities

        /// <summary>
        /// Can produce textual output.
        /// </summary>
        TextOutput = 1 << 4,

        /// <summary>
        /// Can generate images as output.
        /// </summary>
        ImageOutput = 1 << 5,

        /// <summary>
        /// Can produce audio as output (e.g., text-to-speech).
        /// </summary>
        AudioOutput = 1 << 6,

        /// <summary>
        /// Can produce structured JSON output.
        /// </summary>
        JsonOutput = 1 << 7,

        // Advanced capabilities

        /// <summary>
        /// Supports tool/function calling with structured arguments.
        /// </summary>
        FunctionCalling = 1 << 8,

        /// <summary>
        /// Enhanced reasoning capabilities (e.g., long deliberation or thinking).
        /// </summary>
        Reasoning = 1 << 9,

        // Composite capabilities for default definition

        /// <summary>
        /// Text-in to text-out chat capability.
        /// </summary>
        Text2Text = TextInput | TextOutput,

        /// <summary>
        /// Text chat with tool/function calling.
        /// </summary>
        ToolChat = Text2Text | FunctionCalling,

        /// <summary>
        /// Text chat with enhanced reasoning.
        /// </summary>
        ReasoningChat = Text2Text | Reasoning,

        /// <summary>
        /// Text chat with both enhanced reasoning and tool/function calling.
        /// </summary>
        ToolReasoningChat = Text2Text | Reasoning | FunctionCalling,

        /// <summary>
        /// Text-in to JSON-out (structured output generation).
        /// </summary>
        Text2Json = TextInput | JsonOutput,

        /// <summary>
        /// Text-in to image-out (image generation).
        /// </summary>
        Text2Image = TextInput | ImageOutput,

        /// <summary>
        /// Text-in to audio-out (text-to-speech).
        /// </summary>
        Text2Speech = TextInput | AudioOutput,

        /// <summary>
        /// Audio-in to text-out (automatic speech recognition).
        /// </summary>
        Speech2Text = AudioInput | TextOutput,

        /// <summary>
        /// Image-in to text-out (image description or understanding, vision capabilities).
        /// </summary>
        Image2Text = ImageInput | TextOutput,

        /// <summary>
        /// Image-in to image-out (image editing or transformation).
        /// </summary>
        Image2Image = ImageInput | ImageOutput,
    }

    /// <summary>
    /// Extension methods for AICapability.
    /// </summary>
    public static class AICapabilityExtensions
    {
        /// <summary>
        /// Formats AICapability flags for clear logging, showing all individual flags.
        /// </summary>
        /// <param name="capabilities">The capabilities to format.</param>
        /// <returns>A string listing all individual capability flags.</returns>
        public static string ToDetailedString(this AICapability capabilities)
        {
            if (capabilities == AICapability.None)
            {
                return "None";
            }

            var flags = new List<string>();

            // Check each individual flag
            if ((capabilities & AICapability.TextInput) == AICapability.TextInput)
            {
                flags.Add("TextInput");
            }

            if ((capabilities & AICapability.TextOutput) == AICapability.TextOutput)
            {
                flags.Add("TextOutput");
            }

            if ((capabilities & AICapability.ImageInput) == AICapability.ImageInput)
            {
                flags.Add("ImageInput");
            }

            if ((capabilities & AICapability.ImageOutput) == AICapability.ImageOutput)
            {
                flags.Add("ImageOutput");
            }

            if ((capabilities & AICapability.AudioInput) == AICapability.AudioInput)
            {
                flags.Add("AudioInput");
            }

            if ((capabilities & AICapability.AudioOutput) == AICapability.AudioOutput)
            {
                flags.Add("AudioOutput");
            }

            if ((capabilities & AICapability.JsonInput) == AICapability.JsonInput)
            {
                flags.Add("JsonInput");
            }

            if ((capabilities & AICapability.JsonOutput) == AICapability.JsonOutput)
            {
                flags.Add("JsonOutput");
            }

            if ((capabilities & AICapability.FunctionCalling) == AICapability.FunctionCalling)
            {
                flags.Add("FunctionCalling");
            }

            if ((capabilities & AICapability.Reasoning) == AICapability.Reasoning)
            {
                flags.Add("Reasoning");
            }

            return flags.Count > 0 ? string.Join(", ", flags) : "Unknown";
        }

        /// <summary>
        /// Checks if the capability has an input capability.
        /// </summary>
        /// <param name="capability">The capability to check.</param>
        /// <returns>True if the capability has an input capability.</returns>
        public static bool HasInput(this AICapability capability)
        {
            return (capability & AICapability.TextInput) == AICapability.TextInput ||
                   (capability & AICapability.ImageInput) == AICapability.ImageInput ||
                   (capability & AICapability.AudioInput) == AICapability.AudioInput ||
                   (capability & AICapability.JsonInput) == AICapability.JsonInput;
        }

        /// <summary>
        /// Checks if the capability has an output capability.
        /// </summary>
        /// <param name="capability">The capability to check.</param>
        /// <returns>True if the capability has an output capability.</returns>
        public static bool HasOutput(this AICapability capability)
        {
            return (capability & AICapability.TextOutput) == AICapability.TextOutput ||
                   (capability & AICapability.ImageOutput) == AICapability.ImageOutput ||
                   (capability & AICapability.AudioOutput) == AICapability.AudioOutput ||
                   (capability & AICapability.JsonOutput) == AICapability.JsonOutput;
        }

        /// <summary>
        /// Checks if the capability has a specific flag.
        /// </summary>
        /// <param name="capability">The capability to check.</param>
        /// <param name="flag">The flag to check.</param>
        /// <returns>True if the capability has the specified flag.</returns>
        public static bool HasFlag(this AICapability capability, AICapability flag)
        {
            return (capability & flag) == flag;
        }
    }
}
