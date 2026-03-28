// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace QuickWindows.Mouse
{
    public class MouseButtonEventArgs(int x, int y, MouseButton button) : MouseMoveEventArgs(x, y)
    {
        public MouseButton Button { get; } = button;

        /// <summary>
        /// Set to true by the event handler to suppress the event from reaching other applications.
        /// </summary>
        public bool Handled { get; set; }
    }
}
