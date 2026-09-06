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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Interactions
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="OpenAICompatibleAudioCodec"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class OpenAICompatibleAudioCodecTests
    {
        [Theory(DisplayName = "MapAudioFormat maps MIME types to OpenAI format tokens")]
        [InlineData("audio/wav", "wav")]
        [InlineData("audio/x-wav", "wav")]
        [InlineData("audio/mpeg", "mp3")]
        [InlineData("audio/mp3", "mp3")]
        [InlineData("audio/ogg", "opus")]
        [InlineData("audio/opus", "opus")]
        [InlineData("audio/flac", "flac")]
        [InlineData("audio/aac", "aac")]
        [InlineData("audio/mp4", "aac")]
        [InlineData("audio/x-m4a", "aac")]
        [InlineData("audio/pcm", "pcm16")]
        [InlineData("audio/l16", "pcm16")]
        [InlineData("audio/ogg;codecs=opus", "opus")]
        [InlineData(null, "wav")]
        [InlineData("application/octet-stream", "wav")]
        public void MapAudioFormat_MapsMimeTypes(string mimeType, string expected)
        {
            Assert.Equal(expected, OpenAICompatibleAudioCodec.MapAudioFormat(mimeType));
        }

        [Theory(DisplayName = "MapAudioFormat falls back to file extension")]
        [InlineData("voice.mp3", "mp3")]
        [InlineData("clip.m4a", "aac")]
        [InlineData("note.flac", "flac")]
        [InlineData("sound.wav", "wav")]
        public void MapAudioFormat_FallsBackToExtension(string filePath, string expected)
        {
            Assert.Equal(expected, OpenAICompatibleAudioCodec.MapAudioFormat(null, filePath));
        }

        [Fact(DisplayName = "ToInputAudioContentPart emits the OpenAI input_audio shape")]
        public void ToInputAudioContentPart_EmitsInputAudioPart()
        {
            var bytes = new byte[] { 1, 2, 3, 4 };
            var audio = new AIInteractionAudio
            {
                Agent = AIAgent.User,
                Data = bytes,
                MimeType = "audio/mpeg",
            };

            var part = OpenAICompatibleAudioCodec.ToInputAudioContentPart(audio);

            Assert.NotNull(part);
            Assert.Equal("input_audio", part["type"]?.ToString());
            var inputAudio = part["input_audio"] as JObject;
            Assert.NotNull(inputAudio);
            Assert.Equal(Convert.ToBase64String(bytes), inputAudio["data"]?.ToString());
            Assert.Equal("mp3", inputAudio["format"]?.ToString());
        }

        [Fact(DisplayName = "ToInputAudioContentPart returns null when no data is resolvable")]
        public void ToInputAudioContentPart_ReturnsNullWithoutData()
        {
            var audio = new AIInteractionAudio
            {
                Agent = AIAgent.User,
                MimeType = "audio/wav",
            };

            Assert.Null(OpenAICompatibleAudioCodec.ToInputAudioContentPart(audio));
        }

        [Fact(DisplayName = "ApplyAudioOutputParameters adds modalities and audio config")]
        public void ApplyAudioOutputParameters_AddsModalitiesAndAudio()
        {
            var body = new JObject();

            OpenAICompatibleAudioCodec.ApplyAudioOutputParameters(body, null);

            var modalities = body["modalities"] as JArray;
            Assert.NotNull(modalities);
            Assert.Equal("text", modalities[0]?.ToString());
            Assert.Equal("audio", modalities[1]?.ToString());
            var audio = body["audio"] as JObject;
            Assert.NotNull(audio);
            Assert.Equal(OpenAICompatibleAudioCodec.DefaultVoice, audio["voice"]?.ToString());
            Assert.Equal(OpenAICompatibleAudioCodec.DefaultFormat, audio["format"]?.ToString());
        }

        [Fact(DisplayName = "ApplyAudioOutputParameters honors voice and format extras")]
        public void ApplyAudioOutputParameters_HonorsExtras()
        {
            var body = new JObject();
            var extras = new Dictionary<string, JToken>
            {
                ["voice"] = "nova",
                ["format"] = "mp3",
            };

            OpenAICompatibleAudioCodec.ApplyAudioOutputParameters(body, extras);

            var audio = body["audio"] as JObject;
            Assert.Equal("nova", audio?["voice"]?.ToString());
            Assert.Equal("mp3", audio?["format"]?.ToString());
        }

        [Fact(DisplayName = "ExtractAudioOutput parses message.audio data and transcript")]
        public void ExtractAudioOutput_ParsesAudioAndTranscript()
        {
            var bytes = new byte[] { 9, 8, 7 };
            var message = new JObject
            {
                ["audio"] = new JObject
                {
                    ["data"] = Convert.ToBase64String(bytes),
                    ["transcript"] = "hello world",
                },
            };

            var audio = OpenAICompatibleAudioCodec.ExtractAudioOutput(message, out var transcript);

            Assert.NotNull(audio);
            Assert.Equal(AIAgent.Assistant, audio.Agent);
            Assert.Equal(bytes, audio.Data);
            Assert.Equal("hello world", transcript);
        }

        [Fact(DisplayName = "ExtractAudioOutput returns null when no audio is present")]
        public void ExtractAudioOutput_ReturnsNullWithoutAudio()
        {
            var message = new JObject { ["content"] = "plain text" };

            var audio = OpenAICompatibleAudioCodec.ExtractAudioOutput(message, out var transcript);

            Assert.Null(audio);
            Assert.Null(transcript);
        }
    }
}
