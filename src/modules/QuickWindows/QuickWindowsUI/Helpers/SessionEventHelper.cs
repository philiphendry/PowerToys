// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using QuickWindows.Telemetry;

namespace QuickWindows.Helpers;

public static class SessionEventHelper
{
    public static QuickWindowsSession? Event { get; private set; }

    public static void Start()
    {
        Event = new QuickWindowsSession();
        _startTime = DateTime.Now;
    }

    public static void End()
    {
        if (_startTime == null || Event == null)
        {
            Logger.LogError("Failed to send QuickWindowsSessionEvent");
            return;
        }

        var duration = DateTime.Now - _startTime.Value;
        Event.Duration = duration.Seconds + (duration.Milliseconds == 0 ? 0 : 1);
        _startTime = null;
        PowerToysTelemetry.Log.WriteEvent(Event);
    }

    private static DateTime? _startTime;
}
