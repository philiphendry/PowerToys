// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace QuickWindows.Features;

public class SnappingWindows : ISnappingWindows
{
    private readonly IWindowHelpers _windowHelpers;
    private int _snappingThreshold;
    private int _snappingPadding;
    private List<NativeMethods.Rect> _windows = default!;

    public SnappingWindows(IUserSettings userSettings, IWindowHelpers windowHelpers)
    {
        _windowHelpers = windowHelpers;

        _snappingThreshold = userSettings.SnappingThreshold.Value;
        _snappingPadding = userSettings.SnappingPadding.Value;

        userSettings.SnappingThreshold.PropertyChanged += (_, _) => _snappingThreshold = userSettings.SnappingThreshold.Value;
        userSettings.SnappingPadding.PropertyChanged += (_, _) => _snappingPadding = userSettings.SnappingPadding.Value;
    }

    public void StartSnap()
    {
        _windows = _windowHelpers.GetOpenWindows();
        foreach (var window in _windows)
        {
            Logger.LogDebug($"Window ({window.left}, {window.top}, {window.right}, {window.bottom})");
        }
    }

    public (int Left, int Top, int Right, int Bottom) SnapMovingWindow(int left, int top, int right, int bottom)
    {
        var width = right - left;
        var height = bottom - top;
        var newLeft = left;
        int newTop = top;
        int newRight = right;
        int newBottom = bottom;

        var horizontalIntersectingWindows = _windows.FindAll(window =>
                (window.top - _snappingThreshold < top && window.bottom + _snappingThreshold > top)
                || (window.top - _snappingThreshold < bottom & window.bottom + _snappingThreshold > bottom))
            .ToList();

        var closestWindow = FindClosestWithinSnappingThreshold(left, rect => rect.right, horizontalIntersectingWindows, (source, target) => source - target);
        if (closestWindow != null)
        {
            newLeft = closestWindow.Value.right + _snappingPadding;
            newRight = newLeft + width;
        }

        closestWindow = FindClosestWithinSnappingThreshold(right, rect => rect.left, horizontalIntersectingWindows, (source, target) => target - source);
        if (closestWindow != null)
        {
            newRight = closestWindow.Value.left - _snappingPadding;
            newLeft = newRight - width;
        }

        var verticalIntersectingWindows = _windows.FindAll(window =>
           (window.left - _snappingThreshold < left && window.right + _snappingThreshold > left)
            || (window.left - _snappingThreshold < right & window.right + _snappingThreshold > right))
            .ToList();

        closestWindow = FindClosestWithinSnappingThreshold(top, rect => rect.bottom, verticalIntersectingWindows, (source, target) => source - target);
        if (closestWindow != null)
        {
            newTop = closestWindow.Value.bottom + _snappingPadding;
            newBottom = newTop + height;
        }

        closestWindow = FindClosestWithinSnappingThreshold(bottom, rect => rect.top, verticalIntersectingWindows, (source, target) => target - source);

        if (closestWindow != null)
        {
            newBottom = closestWindow.Value.top - _snappingPadding;
            newTop = newBottom - height;
        }

        return (newLeft, newTop, newRight, newBottom);
    }

    private NativeMethods.Rect? FindClosestWithinSnappingThreshold(
        int reference,
        Func<NativeMethods.Rect, int> compareTo,
        List<NativeMethods.Rect> windows,
        Func<int, int, int> calculateGap)
    {
        NativeMethods.Rect? closestWindow = null;
        var closestDistance = int.MaxValue;

        foreach (var window in windows)
        {
            var distance = calculateGap(reference, compareTo(window));
            if (distance >= 0 && distance < closestDistance && distance <= _snappingThreshold)
            {
                closestDistance = distance;
                closestWindow = window;
            }
        }

        return closestWindow;
    }
}
