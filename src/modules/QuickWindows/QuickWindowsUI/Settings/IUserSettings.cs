// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using QuickWindows.Common;

namespace QuickWindows.Settings
{
    public interface IUserSettings
    {
        SettingItem<string> ActivationShortcut { get; }

        void SendSettingsTelemetry();
    }
}
