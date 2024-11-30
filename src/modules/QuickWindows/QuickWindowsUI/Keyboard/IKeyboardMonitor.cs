// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Keyboard;

public interface IKeyboardMonitor
{
    event EventHandler<HotKeyEventArgs> HotKeyPressed;

    event EventHandler<HotKeyEventArgs> HotKeyReleased;

    void Install();

    void Uninstall();

    void SendControlKey();
}
