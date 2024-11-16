// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Mouse;

public interface IMouseHook
{
    event EventHandler<MouseHook.MouseMoveEventArgs>? MouseMove;

    event EventHandler<MouseHook.MouseButtonEventArgs>? MouseDown;

    event EventHandler<MouseHook.MouseButtonEventArgs>? MouseUp;

    event EventHandler<MouseHook.MouseMoveWheelEventArgs>? MouseWheel;

    void Install(bool eventPropagation = false);

    void Uninstall();
}
