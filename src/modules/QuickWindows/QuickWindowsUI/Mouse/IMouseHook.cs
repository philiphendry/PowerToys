// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Mouse;

public interface IMouseHook
{
    event EventHandler<MouseHook.MouseEventArgs>? MouseMove;

    event EventHandler<MouseHook.MouseEventArgs>? MouseDown;

    event EventHandler<MouseHook.MouseEventArgs>? MouseUp;

    event EventHandler<MouseHook.MouseWheelEventArgs>? MouseWheel;

    bool SuppressLeftClick { get; set; }

    void Install();

    void Uninstall();
}
