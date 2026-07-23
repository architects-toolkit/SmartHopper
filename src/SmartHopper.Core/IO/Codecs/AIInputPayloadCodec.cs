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
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Core.IO.Codecs
{
    /// <summary>
    /// Codec for GH_AIInputPayload goo values.
    /// Serializes and deserializes AIInputPayload including polymorphic IAIInteraction instances.
    /// </summary>
    internal sealed class AIInputPayloadCodec : IGooCodec
    {
        public string TypeHint => "GH_AIInputPayload";
        public int Priority => 0;

        public bool CanEncode(IGH_Goo goo) => goo is GH_AIInputPayload payload && payload.Value != null;

        public string Encode(IGH_Goo goo)
        {
            var payload = ((GH_AIInputPayload)goo).Value;
            var json = new JObject
            {
                ["capability"] = payload.InputCapabilityAtSource.ToString(),
                ["payloadType"] = payload.PayloadType.ToString(),
                ["hint"] = payload.Hint
            };

            var interactionsArray = new JArray();
            foreach (var interaction in payload.Interactions)
            {
                var interactionJson = EncodeInteraction(interaction);
                if (interactionJson != null)
                {
                    interactionsArray.Add(interactionJson);
                }
            }
            json["interactions"] = interactionsArray;

            return json.ToString(Formatting.None);
        }

        public bool TryDecode(string data, out IGH_Goo goo, out string warning)
        {
            warning = null;
            try
            {
                var json = JObject.Parse(data);
                var capability = Enum.TryParse<AICapability>(json.Value<string>("capability") ?? string.Empty, out var cap) ? cap : AICapability.None;
                var payloadType = Enum.TryParse<AIInputPayloadType>(json.Value<string>("payloadType") ?? string.Empty, out var pt) ? pt : AIInputPayloadType.Unknown;
                var hint = json.Value<string>("hint");

                var interactions = new List<IAIInteraction>();
                var interactionsArray = json["interactions"] as JArray;
                if (interactionsArray != null)
                {
                    foreach (var interactionJson in interactionsArray.OfType<JObject>())
                    {
                        var interaction = DecodeInteraction(interactionJson);
                        if (interaction != null)
                        {
                            interactions.Add(interaction);
                        }
                    }
                }

                var payload = new AIInputPayload(interactions, capability, payloadType, hint);
                goo = new GH_AIInputPayload(payload);
                return true;
            }
            catch (Exception ex)
            {
                goo = new GH_String(data);
                warning = $"Failed to decode GH_AIInputPayload; restored as GH_String. {ex.Message}";
                return true;
            }
        }

        private JObject EncodeInteraction(IAIInteraction interaction)
        {
            var json = new JObject();

            // Common fields
            json["turnId"] = interaction.TurnId;
            json["time"] = interaction.Time.ToString("O");
            json["agent"] = interaction.Agent.ToString();

            switch (interaction)
            {
                case AIInteractionText text:
                    json["$type"] = "text";
                    json["content"] = text.Content;
                    json["reasoning"] = text.Reasoning;
                    break;

                case AIInteractionImage img:
                    json["$type"] = "image";
                    json["imageUrl"] = img.ImageUrl?.ToString();
                    json["imageData"] = img.ImageData;
                    json["revisedPrompt"] = img.RevisedPrompt;
                    json["originalPrompt"] = img.OriginalPrompt;
                    json["imageSize"] = img.ImageSize;
                    json["imageQuality"] = img.ImageQuality;
                    json["imageStyle"] = img.ImageStyle;
                    json["mimeType"] = img.MimeType;
                    break;

                case AIInteractionAudio aud:
                    json["$type"] = "audio";
                    json["data"] = aud.Data != null ? Convert.ToBase64String(aud.Data) : null;
                    json["filePath"] = aud.FilePath;
                    json["mimeType"] = aud.MimeType;
                    json["languageHint"] = aud.LanguageHint;
                    break;

                case AIInteractionToolResult toolResult:
                    json["$type"] = "toolResult";
                    json["id"] = toolResult.Id;
                    json["name"] = toolResult.Name;
                    json["arguments"] = toolResult.Arguments?.ToString(Formatting.None);
                    json["reasoning"] = toolResult.Reasoning;
                    json["result"] = toolResult.Result?.ToString(Formatting.None);
                    break;

                case AIInteractionToolCall toolCall:
                    json["$type"] = "toolCall";
                    json["id"] = toolCall.Id;
                    json["name"] = toolCall.Name;
                    json["arguments"] = toolCall.Arguments?.ToString(Formatting.None);
                    json["reasoning"] = toolCall.Reasoning;
                    break;

                case AIInteractionRuntimeMessage msg:
                    json["$type"] = "runtimeMessage";
                    json["severity"] = msg.Severity.ToString();
                    json["code"] = msg.Code.ToString();
                    json["origin"] = msg.Origin.ToString();
                    json["surfaceable"] = msg.Surfaceable;
                    json["content"] = msg.Content;
                    break;

                default:
                    // Unknown interaction type - skip
                    return null;
            }

            return json;
        }

        private IAIInteraction DecodeInteraction(JObject json)
        {
            var type = json.Value<string>("$type");
            if (string.IsNullOrEmpty(type))
                return null;

            var turnId = json.Value<string>("turnId");
            var timeStr = json.Value<string>("time");
            var agent = Enum.TryParse<AIAgent>(json.Value<string>("agent") ?? string.Empty, out var ag) ? ag : AIAgent.User;

            IAIInteraction interaction = type switch
            {
                "text" => new AIInteractionText
                {
                    Content = json.Value<string>("content"),
                    Reasoning = json.Value<string>("reasoning")
                },
                "image" => new AIInteractionImage
                {
                    ImageUrl = !string.IsNullOrWhiteSpace(json.Value<string>("imageUrl")) ? new Uri(json.Value<string>("imageUrl")) : null,
                    ImageData = json.Value<string>("imageData"),
                    RevisedPrompt = json.Value<string>("revisedPrompt"),
                    OriginalPrompt = json.Value<string>("originalPrompt"),
                    ImageSize = json.Value<string>("imageSize") ?? "1024x1024",
                    ImageQuality = json.Value<string>("imageQuality") ?? "standard",
                    ImageStyle = json.Value<string>("imageStyle") ?? "vivid",
                    MimeType = json.Value<string>("mimeType")
                },
                "audio" => new AIInteractionAudio
                {
                    Data = json.Value<string>("data") != null ? Convert.FromBase64String(json.Value<string>("data")) : null,
                    FilePath = json.Value<string>("filePath"),
                    MimeType = json.Value<string>("mimeType"),
                    LanguageHint = json.Value<string>("languageHint")
                },
                "toolCall" => new AIInteractionToolCall
                {
                    Id = json.Value<string>("id"),
                    Name = json.Value<string>("name"),
                    Arguments = json.Value<string>("arguments") != null ? JObject.Parse(json.Value<string>("arguments")) : null,
                    Reasoning = json.Value<string>("reasoning")
                },
                "toolResult" => new AIInteractionToolResult
                {
                    Id = json.Value<string>("id"),
                    Name = json.Value<string>("name"),
                    Arguments = json.Value<string>("arguments") != null ? JObject.Parse(json.Value<string>("arguments")) : null,
                    Reasoning = json.Value<string>("reasoning"),
                    Result = json.Value<string>("result") != null ? JObject.Parse(json.Value<string>("result")) : null
                },
                "runtimeMessage" => new AIInteractionRuntimeMessage
                {
                    Severity = Enum.TryParse<SmartHopper.Infrastructure.Diagnostics.SHRuntimeMessageSeverity>(json.Value<string>("severity") ?? string.Empty, out var sev) ? sev : SmartHopper.Infrastructure.Diagnostics.SHRuntimeMessageSeverity.Info,
                    Code = Enum.TryParse<SmartHopper.Infrastructure.Diagnostics.SHMessageCode>(json.Value<string>("code") ?? string.Empty, out var code) ? code : SmartHopper.Infrastructure.Diagnostics.SHMessageCode.Unknown,
                    Origin = Enum.TryParse<SmartHopper.Infrastructure.Diagnostics.SHRuntimeMessageOrigin>(json.Value<string>("origin") ?? string.Empty, out var orig) ? orig : SmartHopper.Infrastructure.Diagnostics.SHRuntimeMessageOrigin.Worker,
                    Surfaceable = json.Value<bool>("surfaceable"),
                    Content = json.Value<string>("content")
                },
                _ => null
            };

            if (interaction is AIInteractionBase baseInteraction)
            {
                baseInteraction.TurnId = turnId;
                baseInteraction.Time = DateTime.TryParse(timeStr, out var time) ? time : DateTime.UtcNow;
                baseInteraction.Agent = agent;
            }

            return interaction;
        }
    }
}
