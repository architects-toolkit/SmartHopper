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

using System.Collections.Generic;
using GhJSON.Core.Models.Components;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    public static class ScriptParameterSettingsParser
    {
        public static List<ParameterSettings>? ConvertToParameterSettings(JArray? parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return null;
            }

            var result = new List<ParameterSettings>();
            foreach (var param in parameters)
            {
                if (param is not JObject obj)
                {
                    continue;
                }

                var name = obj["name"]?.ToString();
                var variableName = obj["variableName"]?.ToString() ?? name;

                var settings = new ParameterSettings
                {
                    ParameterName = name ?? "param",
                    VariableName = variableName,
                    Description = obj["description"]?.ToString(),
                    TypeHint = obj["type"]?.ToString(),
                    Access = obj["access"]?.ToString(),
                    DataMapping = obj["dataMapping"]?.ToString(),
                    Required = obj["required"]?.ToObject<bool?>(),
                    IsPrincipal = obj["isPrincipal"]?.ToObject<bool?>(),
                    Expression = obj["expression"]?.ToString(),
                };

                var reverse = obj["reverse"]?.ToObject<bool?>();
                var simplify = obj["simplify"]?.ToObject<bool?>();
                var invert = obj["invert"]?.ToObject<bool?>();
                if (reverse == true || simplify == true || invert == true)
                {
                    settings.AdditionalSettings = new AdditionalParameterSettings
                    {
                        Reverse = reverse == true ? true : null,
                        Simplify = simplify == true ? true : null,
                        Invert = invert == true ? true : null,
                    };
                }

                result.Add(settings);
            }

            return result.Count > 0 ? result : null;
        }
    }
}
