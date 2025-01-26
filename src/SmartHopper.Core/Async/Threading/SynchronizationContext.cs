/* DEPRECATED */

/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Threading;
using Rhino;

namespace SmartHopper.Core.Async.Threading
{
    public class GrasshopperSynchronizationContext
    {
        public static void PostToUI(Action action)
        {
            RhinoApp.InvokeOnUiThread(action);
        }

        public static void PostToUI<T>(Action<T> action, T state)
        {
            RhinoApp.InvokeOnUiThread(new Action(() => action(state)));
        }

        public static void EnsureOnUIThread(Action action)
        {
            if (Thread.CurrentThread.IsBackground)
            {
                PostToUI(action);
            }
            else
            {
                action();
            }
        }
    }
}
