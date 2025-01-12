// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Keyboard;

public interface IGlobalKeyboardHook : IDisposable
{
    event EventHandler<GlobalKeyboardHookEventArgs>? KeyboardPressed;
}
