// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Interfaces;

public interface ITargetWindow
{
    void SetTargetWindow(int x, int y);

    bool HaveTargetWindow { get; }

    IntPtr HWnd { get; }

    NativeMethods.Rect InitialPlacement { get; }

    void ClearTargetWindow();

    void SetInitialPlacement(NativeMethods.Rect placement);
}
