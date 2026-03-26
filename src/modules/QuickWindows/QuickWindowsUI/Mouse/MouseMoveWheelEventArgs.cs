// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace QuickWindows.Mouse
{
    public class MouseMoveWheelEventArgs(int x, int y, int delta) : MouseMoveEventArgs(x, y)
    {
        public int Delta { get; } = delta;

        /// <summary>
        /// Set to true by the event handler to suppress the event from reaching other applications.
        /// </summary>
        public bool Handled { get; set; }
    }
}
