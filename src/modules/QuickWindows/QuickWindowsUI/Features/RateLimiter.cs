// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;

namespace QuickWindows.Features;

[Export(typeof(IRateLimiter))]
public class RateLimiter : IRateLimiter
{
    private const int MinUpdateIntervalMs = 32; // Approx. 30fps
    private long _lastUpdateTime = Environment.TickCount64;

    public bool IsLimited()
    {
        var now = Environment.TickCount64;
        if ((now - _lastUpdateTime) < MinUpdateIntervalMs)
        {
            return true;
        }

        _lastUpdateTime = now;
        return false;
    }
}
