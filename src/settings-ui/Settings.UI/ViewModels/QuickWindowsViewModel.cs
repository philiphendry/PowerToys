// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Timers;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class QuickWindowsViewModel : Observable, IDisposable
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private bool disposedValue;

        // Delay saving of settings in order to avoid calling save multiple times and hitting file in use exception. If there is no other request to save settings in given interval, we proceed to save it, otherwise we schedule saving it after this interval
        private const int SaveSettingsDelayInMs = 500;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;
        private readonly object _delayedActionLock = new object();

        private QuickWindowsSettings _quickWindowsSettings;
        private Timer _delayedTimer;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        private Func<string, int> SendConfigMSG { get; }

        public QuickWindowsViewModel(
            ISettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            ISettingsRepository<QuickWindowsSettings> quickWindowsSettingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            // Obtain the general PowerToy settings configurations
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            if (quickWindowsSettingsRepository == null)
            {
                // used in release. This method converts the settings stored in the previous form, so we have forwards compatibility
                _quickWindowsSettings = _settingsUtils.GetSettingsOrDefault<QuickWindowsSettings>(QuickWindowsSettings.ModuleName);
            }
            else
            {
                _quickWindowsSettings = quickWindowsSettingsRepository.SettingsConfig; // used in the unit tests
            }

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _delayedTimer = new Timer();
            _delayedTimer.Interval = SaveSettingsDelayInMs;
            _delayedTimer.Elapsed += DelayedTimer_Tick;
            _delayedTimer.AutoReset = false;
        }

        public void LoadUpdatedSettings()
        {
            try
            {
                _quickWindowsSettings = _settingsUtils.GetSettings<QuickWindowsSettings>("QuickWindows");
                OnPropertyChanged(null); // Notify all properties might have changed.
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredQuickWindowsEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.QuickWindows;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    // Set the status of QuickWindows in the general settings
                    GeneralSettingsConfig.Enabled.QuickWindows = value;
                    var outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool ActivateOnAlt
        {
            get => _quickWindowsSettings.Properties.ActivateOnAlt;

            set
            {
                if (_quickWindowsSettings.Properties.ActivateOnAlt != value)
                {
                    _quickWindowsSettings.Properties.ActivateOnAlt = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        public bool ActivateOnShift
        {
            get => _quickWindowsSettings.Properties.ActivateOnShift;

            set
            {
                if (_quickWindowsSettings.Properties.ActivateOnShift != value)
                {
                    _quickWindowsSettings.Properties.ActivateOnShift = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        public bool ActivateOnCtrl
        {
            get => _quickWindowsSettings.Properties.ActivateOnCtrl;

            set
            {
                if (_quickWindowsSettings.Properties.ActivateOnCtrl != value)
                {
                    _quickWindowsSettings.Properties.ActivateOnCtrl = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        public bool DoNotActivateOnGameMode
        {
            get => _quickWindowsSettings.Properties.DoNotActivateOnGameMode;

            set
            {
                if (_quickWindowsSettings.Properties.DoNotActivateOnGameMode != value)
                {
                    _quickWindowsSettings.Properties.DoNotActivateOnGameMode = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        public bool TransparentWindowOnMove
        {
            get => _quickWindowsSettings.Properties.TransparentWindowOnMove;

            set
            {
                if (_quickWindowsSettings.Properties.TransparentWindowOnMove != value)
                {
                    _quickWindowsSettings.Properties.TransparentWindowOnMove = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        public bool RolodexEnabled
        {
            get => _quickWindowsSettings.Properties.RolodexEnabled;

            set
            {
                if (_quickWindowsSettings.Properties.RolodexEnabled != value)
                {
                    _quickWindowsSettings.Properties.RolodexEnabled = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        public bool SnappingEnabled
        {
            get => _quickWindowsSettings.Properties.SnappingEnabled;

            set
            {
                if (_quickWindowsSettings.Properties.SnappingEnabled != value)
                {
                    _quickWindowsSettings.Properties.SnappingEnabled = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        public int SnapGap
        {
            get => _quickWindowsSettings.Properties.SnappingPadding;

            set
            {
                if (_quickWindowsSettings.Properties.SnappingPadding != value)
                {
                    _quickWindowsSettings.Properties.SnappingPadding = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        public bool ExcludeAppDetection
        {
            get => _quickWindowsSettings.Properties.ExcludeAppDetection;

            set
            {
                if (_quickWindowsSettings.Properties.ExcludeAppDetection != value)
                {
                    _quickWindowsSettings.Properties.ExcludeAppDetection = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        public string ExcludedApps
        {
            get
            {
                return _quickWindowsSettings.Properties.ExcludedApplications;
            }

            set
            {
                if (_quickWindowsSettings.Properties.ExcludedApplications != value)
                {
                    _quickWindowsSettings.Properties.ExcludedApplications = value;
                    OnPropertyChanged();
                    ScheduleSavingOfSettings();
                }
            }
        }

        private void ScheduleSavingOfSettings()
        {
            lock (_delayedActionLock)
            {
                if (_delayedTimer.Enabled)
                {
                    _delayedTimer.Stop();
                }

                _delayedTimer.Start();
            }
        }

        private void DelayedTimer_Tick(object sender, EventArgs e)
        {
            lock (_delayedActionLock)
            {
                _delayedTimer.Stop();
                NotifySettingsChanged();
            }
        }

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       QuickWindowsSettings.ModuleName,
                       JsonSerializer.Serialize(_quickWindowsSettings, _serializerOptions)));
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _delayedTimer.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
