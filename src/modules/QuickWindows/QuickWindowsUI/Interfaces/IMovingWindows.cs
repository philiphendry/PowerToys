// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace QuickWindows.Interfaces;

public interface IMovingWindows
{
    void StartMove(int x, int y);

    void MoveWindow(int x, int y);

    void StopMove();
}
