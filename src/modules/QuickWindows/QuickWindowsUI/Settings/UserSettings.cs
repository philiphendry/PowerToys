// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Telemetry;

namespace QuickWindows.Settings;

public class UserSettings : IUserSettings
{
    private readonly object _loadingSettingsLock = new();
    private readonly SettingsUtils _settingsUtils;
    private readonly IFileSystemWatcher _watcher;
    private const string QuickWindowsModuleName = "QuickWindows";
    private const int MaxNumberOfRetry = 5;
    private const int SettingsReadOnChangeDelayInMs = 300;

    public event EventHandler? Changed;

    public UserSettings(Helpers.IThrottledActionInvoker throttledActionInvoker)
    {
        _settingsUtils = new SettingsUtils();

        LoadSettingsFromJson();

        // delay loading settings on change by some time to avoid file in use exception
        _watcher = Helper.GetFileWatcher(QuickWindowsModuleName, "settings.json", () => throttledActionInvoker.ScheduleAction(LoadSettingsFromJson, SettingsReadOnChangeDelayInMs));
    }

    public SettingItem<bool> ActivateOnAlt { get; } = new(true);

    public SettingItem<bool> ActivateOnShift { get; } = new(false);

    public SettingItem<bool> ActivateOnCtrl { get; } = new(false);

    public SettingItem<bool> DoNotActivateOnGameMode { get; } = new(true);

    public SettingItem<bool> TransparentWindowOnMove { get; } = new(true);

    public SettingItem<bool> ExcludeAppDetection { get; } = new(false);

    public SettingItem<string> ExcludedApplications { get; set; } = new(string.Empty);

    public void AddExcludedApplication(string windowTitle, string windowClass)
    {
        try
        {
            var exclusions = ExcludedApplications.Value
                .Split('\r')
                .Where(i => i.Trim().Length > 0)
                .ToList();

            var newExclusion = $"{windowTitle}||{windowClass}";
            if (exclusions.Contains(newExclusion))
            {
                return;
            }

            if (exclusions.Count > 0)
            {
                newExclusion = '\r' + newExclusion;
            }

            ExcludedApplications.Value += newExclusion;
            SaveSettings();
        }
        catch (Exception exception)
        {
            Logger.LogError("Failed to add excluded application", exception);
        }
    }

    private void SaveSettings()
    {
        var settings = new QuickWindowsSettings();
        settings.Properties.ActivateOnAlt = ActivateOnAlt.Value;
        settings.Properties.ActivateOnShift = ActivateOnShift.Value;
        settings.Properties.ActivateOnCtrl = ActivateOnCtrl.Value;
        settings.Properties.DoNotActivateOnGameMode = DoNotActivateOnGameMode.Value;
        settings.Properties.TransparentWindowOnMove = TransparentWindowOnMove.Value;
        settings.Properties.ExcludeAppDetection = ExcludeAppDetection.Value;
        settings.Properties.ExcludedApplications = ExcludedApplications.Value;
        settings.Save(_settingsUtils);
    }

    private void LoadSettingsFromJson()
    {
        lock (_loadingSettingsLock)
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
                        void UpdateSettings()
                        {
                            ActivateOnAlt.Value = settings.Properties.ActivateOnAlt;
                            ActivateOnShift.Value = settings.Properties.ActivateOnShift;
                            ActivateOnCtrl.Value = settings.Properties.ActivateOnCtrl;
                            DoNotActivateOnGameMode.Value = settings.Properties.DoNotActivateOnGameMode;
                            TransparentWindowOnMove.Value = settings.Properties.TransparentWindowOnMove;
                            ExcludeAppDetection.Value = settings.Properties.ExcludeAppDetection;
                            ExcludedApplications.Value = settings.Properties.ExcludedApplications ?? string.Empty;

                            Logger.LogDebug("Publishing changes to settings.");
                            Changed?.Invoke(this, EventArgs.Empty);
                        }

                        Task.Factory.StartNew(UpdateSettings, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext()).Wait();
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
            TransparentWindowOnMove = properties.TransparentWindowOnMove,
        };

        PowerToysTelemetry.Log.WriteEvent(telemetrySettings);
    }
}
