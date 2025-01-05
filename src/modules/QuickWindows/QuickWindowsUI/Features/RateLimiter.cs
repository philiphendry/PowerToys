// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class RateLimiter : IRateLimiter
{
    private long _lastUpdateTime = Environment.TickCount64;

    public int Interval { get; set; } = 32; // Approx. 30fps

    public bool IsLimited()
    {
        var now = Environment.TickCount64;
        if ((now - _lastUpdateTime) < Interval)
        {
            return true;
        }

        _lastUpdateTime = now;
        return false;
    }
}
