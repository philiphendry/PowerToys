// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace QuickWindows.Interfaces;

public interface ISnappingWindows
{
    public void StartSnap();

    public (int Left, int Top, int Right, int Bottom) SnapMovingWindow(int left, int top, int right, int bottom);
}
