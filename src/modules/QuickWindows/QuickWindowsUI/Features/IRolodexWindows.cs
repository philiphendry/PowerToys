// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace QuickWindows.Features;

public interface IRolodexWindows
{
    void SendWindowToBottom(int x, int y);

    void BringBottomWindowToTop(int x, int y);
}
