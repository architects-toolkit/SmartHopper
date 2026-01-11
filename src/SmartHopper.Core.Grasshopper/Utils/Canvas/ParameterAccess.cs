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
using System.Diagnostics;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    public static class ParameterAccess
    {
        public static List<IGH_Param> GetAllInputs(IGH_Component component)
        {
            return component.Params.Input;
        }

        public static List<IGH_Param> GetAllOutputs(IGH_Component component)
        {
            return component.Params.Output;
        }

        public static IGH_Param? GetInputByName(IGH_Component component, string name)
        {
            List<IGH_Param> paramList = GetAllInputs(component);
            foreach (IGH_Param param in paramList)
            {
                if (param.Name == name)
                {
                    return param;
                }
            }

            Debug.WriteLine($"Could not find input named '{name}' in component '{component.InstanceGuid}'");
            return null;
        }

        public static IGH_Param? GetOutputByName(IGH_Component component, string name)
        {
            List<IGH_Param> paramList = GetAllOutputs(component);
            foreach (IGH_Param param in paramList)
            {
                if (param.Name == name)
                {
                    return param;
                }
            }

            Debug.WriteLine($"Could not find output named '{name}' in component '{component.InstanceGuid}'");
            return null;
        }

        public static void SetSource(IGH_Param instance, IGH_Param source)
        {
            instance.AddSource(source);
        }
    }
}
