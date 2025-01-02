/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * Portions of this code adapted from:
 * https://github.com/lamest/AsyncComponent
 * MIT License
 * Copyright (c) 2022 Ivan Sukhikh
 */

using System;

namespace SmartHopper.Core.Async.Components
{
    public interface IAsyncComponent
    {
        bool ITaskCapable { get; }
        bool UseTasks { get; set; }
        void RequestTaskCancellation();
        bool InPreSolve { get; set; }
    }
}
