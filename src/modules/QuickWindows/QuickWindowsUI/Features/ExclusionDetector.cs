// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace QuickWindows.Features;

public class ExclusionDetector : IExclusionDetector
{
    private readonly IWindowHelpers _windowHelpers;
    private readonly IUserSettings _userSettings;
    private bool _isEnabled;

    public ExclusionDetector(
        IWindowHelpers windowHelpers,
        IUserSettings userSettings)
    {
        _windowHelpers = windowHelpers;
        _userSettings = userSettings;
        _userSettings.ExcludeAppDetection.PropertyChanged += ExcludeAppDetection_PropertyChanged;
        _isEnabled = userSettings.ExcludeAppDetection.Value;
    }

    private void ExcludeAppDetection_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _isEnabled = _userSettings.ExcludeAppDetection.Value;
    }

    public bool IsEnabled => _isEnabled;

    public void ExcludeWindowAtCursor()
    {
        var (success, windowTitle, windowClass) = _windowHelpers.GetWindowInfoAtCursor();

        Logger.LogDebug($"Detected window: {windowTitle} - {windowClass} with succes: {success}");
        if (!success)
        {
            return;
        }

        _userSettings.AddExcludedApplication(windowTitle, windowClass);
    }
}
