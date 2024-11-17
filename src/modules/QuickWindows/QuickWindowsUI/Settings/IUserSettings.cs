// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Settings;

public interface IUserSettings
{
    event EventHandler? Changed;

    void SendSettingsTelemetry();

    SettingItem<bool> TransparentWindowOnMove { get; }
}
