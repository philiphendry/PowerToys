// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Mouse
{
    public class MouseMoveEventArgs(int x, int y) : EventArgs
    {
        public int X { get; } = x;

        public int Y { get; } = y;
    }
}
