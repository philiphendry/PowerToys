// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Interfaces;

public interface IFancyZonesBridge
{
    bool IsAvailable { get; }

    void StartMove(IntPtr hwnd);

    void UpdateMove(IntPtr hwnd);

    void EndMove(IntPtr hwnd);
}
