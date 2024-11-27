// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace QuickWindows.Interfaces;

public interface ICursorForOperation
{
    void StartMove(int x, int y);

    void StartResizeNorthWestSouthEast(int x, int y);

    void StartResizeNorthEastSouthWest(int x, int y);

    void StartExclusionDetection(int x, int y);

    void HideCursor();

    void MoveToCursor(int x, int y);
}
