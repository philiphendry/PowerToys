// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace QuickWindows.Settings;

public interface IUserSettings
{
    event EventHandler? Changed;

    SettingItem<bool> ActivateOnAlt { get; }

    SettingItem<bool> ActivateOnShift { get; }

    SettingItem<bool> ActivateOnCtrl { get; }

    SettingItem<bool> DoNotActivateOnGameMode { get; }

    SettingItem<bool> TransparentWindowOnMove { get; }

    SettingItem<bool> ExcludeAppDetection { get; }

    SettingItem<string> ExcludedApplications { get; }

    void AddExcludedApplication(string windowTitle, string windowClass);

    void SendSettingsTelemetry();
}
