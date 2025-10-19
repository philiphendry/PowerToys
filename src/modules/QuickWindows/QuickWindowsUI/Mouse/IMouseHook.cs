// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Mouse;

public interface IMouseHook
{
    event EventHandler<MouseMoveEventArgs>? MouseMove;

    event EventHandler<MouseButtonEventArgs>? MouseDown;

    event EventHandler<MouseButtonEventArgs>? MouseUp;

    event EventHandler<MouseMoveWheelEventArgs>? MouseWheel;

    void Install();

    void Uninstall();

    void EnableEvents();

    void DisableEvents();

    bool Intercepting { get; set; }
}
