// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace QuickWindows.Features;

public class ExclusionFilter : IExclusionFilter
{
    private readonly IWindowHelpers _windowHelpers;
    private readonly IUserSettings _userSettings;
    private List<Exclusion> _exclusions = new();

    public ExclusionFilter(
        IWindowHelpers windowHelpers,
        IUserSettings userSettings)
    {
        _windowHelpers = windowHelpers;
        _userSettings = userSettings;
        _userSettings.ExcludedApplications.PropertyChanged += (_, _) => SetExclusionList();
        SetExclusionList();
    }

    public bool IsWindowAtCursorExcluded()
    {
        var (success, windowTitle, windowClass) = _windowHelpers.GetWindowInfoAtCursor();
        if (!success)
        {
            return false;
        }

        var isWindowAtCursorExcluded = _exclusions.Any(i =>
            (string.IsNullOrEmpty(i.WindowTitle) || i.WindowTitle.Equals(windowTitle, StringComparison.OrdinalIgnoreCase))
            && i.WindowClass.Equals(windowClass, StringComparison.OrdinalIgnoreCase));

        return isWindowAtCursorExcluded;
    }

    private void SetExclusionList()
    {
        _exclusions = _userSettings.ExcludedApplications.Value
            .Split('\r')
            .Where(i => i.Trim().Length > 0)
            .Where(i => i.Contains("||"))
            .Select(i =>
            {
                var parts = i.Split("||");
                return new Exclusion(parts[0], parts[1]);
            })
            .ToList();
    }
}
