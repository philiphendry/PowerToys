// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace QuickWindows.Keyboard;

public sealed class GlobalKeyboardHookEventArgs(
    GlobalKeyboardHook.LowLevelKeyboardInputEvent keyboardData,
    GlobalKeyboardHook.KeyboardState keyboardState)
    : HandledEventArgs
{
    public GlobalKeyboardHook.KeyboardState KeyboardState { get; private set; } = keyboardState;

    public GlobalKeyboardHook.LowLevelKeyboardInputEvent KeyboardData { get; private set; } = keyboardData;
}
