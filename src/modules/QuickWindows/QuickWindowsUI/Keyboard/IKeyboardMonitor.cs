// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Keyboard;

public interface IKeyboardMonitor : IDisposable
{
    event EventHandler HotKeyPressed;

    event EventHandler HotKeyReleased;

    void Install();

    void Uninstall();

    /// <summary>
    /// Sending control key when releasing Alt hot key prevents the window menus being activated.
    /// </summary>
    void SendControlKey();

    bool CheckHotKeyActive();
}
