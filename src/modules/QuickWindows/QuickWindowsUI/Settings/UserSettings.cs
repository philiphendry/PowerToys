// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Telemetry;

namespace QuickWindows.Settings;

[Export(typeof(IUserSettings))]
public class UserSettings : IUserSettings
{
    private readonly SettingsUtils _settingsUtils;
    private const string QuickWindowsModuleName = "QuickWindows";
    private const string DefaultActivationShortcut = "Alt";
    private const int MaxNumberOfRetry = 5;
    private const int SettingsReadOnChangeDelayInMs = 300;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Actually, call back is LoadSettingsFromJson")]
    private readonly IFileSystemWatcher _watcher;

    private readonly object _loadingSettingsLock = new object();

    private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
    };

    [ImportingConstructor]
    public UserSettings(Helpers.IThrottledActionInvoker throttledActionInvoker)
    {
        _settingsUtils = new SettingsUtils();
        ActivationShortcut = new SettingItem<string>(DefaultActivationShortcut);

        LoadSettingsFromJson();

        // delay loading settings on change by some time to avoid file in use exception
        _watcher = Helper.GetFileWatcher(QuickWindowsModuleName, "settings.json", () => throttledActionInvoker.ScheduleAction(LoadSettingsFromJson, SettingsReadOnChangeDelayInMs));
    }

    public SettingItem<string> ActivationShortcut { get; private set; }

    private void LoadSettingsFromJson()
    {
        // TODO this IO call should by Async, update GetFileWatcher helper to support async
        lock (_loadingSettingsLock)
        {
            {
                var retry = true;
                var retryCount = 0;

                while (retry)
                {
                    try
                    {
                        retryCount++;

                        if (!_settingsUtils.SettingsExists(QuickWindowsModuleName))
                        {
                            Logger.LogInfo("QuickWindows settings.json was missing, creating a new one");
                            var defaultQuickWindowsSettings = new QuickWindowsSettings();
                            defaultQuickWindowsSettings.Save(_settingsUtils);
                        }

                        var settings = _settingsUtils.GetSettingsOrDefault<QuickWindowsSettings>(QuickWindowsModuleName);
                        if (settings != null)
                        {
                            ActivationShortcut.Value = settings.Properties.ActivationShortcut.ToString();
                        }

                        retry = false;
                    }
                    catch (IOException ex)
                    {
                        if (retryCount > MaxNumberOfRetry)
                        {
                            retry = false;
                        }

                        Logger.LogError("Failed to read changed settings", ex);
                        Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        if (retryCount > MaxNumberOfRetry)
                        {
                            retry = false;
                        }

                        Logger.LogError("Failed to read changed settings", ex);
                        Thread.Sleep(500);
                    }
                }
            }
        }
    }

    public void SendSettingsTelemetry()
    {
        Logger.LogInfo("Sending settings telemetry");
        var settings = _settingsUtils.GetSettingsOrDefault<QuickWindowsSettings>(QuickWindowsModuleName);
        var properties = settings?.Properties;
        if (properties == null)
        {
            Logger.LogError("Failed to send settings telemetry");
            return;
        }

        var telemetrySettings = new Telemetry.QuickWindowsSettings()
        {
            ActivationShortcut = properties.ActivationShortcut.ToString(),
        };

        PowerToysTelemetry.Log.WriteEvent(telemetrySettings);
    }
}
