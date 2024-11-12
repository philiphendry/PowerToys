// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Media;

namespace QuickWindows.ViewModelContracts
{
    public interface IMainViewModel
    {
        void RegisterWindowHandle(System.Windows.Interop.HwndSource hwndSource);
    }
}
