// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace QuickWindows;

public interface IWindowOperations
{
    void StartOperation(int x, int y, WindowOperation operation);

    void ResizeWindowWithMouse(int x, int y);

    void MoveWindowWithMouse(int x, int y);

    void EndOperation();

    void SendWindowToBottom(int x, int y);

    void BringBottomWindowToTop(int x, int y);
}
