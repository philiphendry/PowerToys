// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using QuickWindows.Helpers;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace QuickWindows.Features;

public class SnappingWindows : ISnappingWindows
{
    private record Snappable(NativeMethods.Rect Rect, bool SnapInside);

    private readonly IWindowHelpers _windowHelpers;
    private readonly IMonitorInfos _monitorInfos;
    private bool _snappingEnabled;
    private int _snappingThreshold;
    private int _snappingPadding;
    private List<Snappable> _monitorAreas = null!;
    private List<Snappable> _snappableAreas = null!;
    private NativeMethods.Rect _windowBorderOffsets;

    public SnappingWindows(
        IUserSettings userSettings,
        IWindowHelpers windowHelpers,
        IMonitorInfos monitorInfos)
    {
        _windowHelpers = windowHelpers;
        _monitorInfos = monitorInfos;

        _snappingEnabled = userSettings.SnappingEnabled.Value;
        _snappingPadding = userSettings.SnappingPadding.Value;
        _snappingThreshold = userSettings.SnappingPadding.Value + 30;

        userSettings.SnappingEnabled.PropertyChanged += (_, _) => _snappingEnabled = userSettings.SnappingEnabled.Value;
        userSettings.SnappingPadding.PropertyChanged += (_, _) =>
        {
            _snappingPadding = userSettings.SnappingPadding.Value;
            _snappingThreshold = userSettings.SnappingPadding.Value + 30;
        };
    }

    public void StartSnap(IntPtr targetWindow)
    {
        if (!_snappingEnabled)
        {
            return;
        }

        _monitorAreas = _monitorInfos
            .GetAllMonitorInfos()
            .Select(mi => new Snappable(mi.WorkingArea, SnapInside: true))
            .ToList();

        var windows = _windowHelpers.GetSnappableWindows(targetWindow);
        _snappableAreas = windows
            .Select(w =>
            {
                if (_snappingPadding > 0)
                {
                    NativeMethods.InflateRect(out w, _snappingPadding, _snappingPadding);
                }

                return new Snappable(w, SnapInside: false);
            })
            .ToList();
        _snappableAreas.AddRange(_monitorAreas);

        _windowBorderOffsets = CalculateBorderOffsets(targetWindow, _snappingPadding);
    }

    private static bool IsInRange(int x, int a, int b, int tolerance) => (a - tolerance <= x) && (x <= b + tolerance);

    private static bool IsEqual(int a, int b, int tolerance) => (b - tolerance <= a) & (a <= b + tolerance);

    private static void SubRect(ref NativeMethods.Rect frame, NativeMethods.Rect rect)
    {
        frame.left -= rect.left;
        frame.top -= rect.top;
        frame.right = rect.right - frame.right;
        frame.bottom = rect.bottom - frame.bottom;
    }

    /// <summary>
    /// Calling GetWindowRect on Windows 10/11 will return the window rect including the shadow so we attempt
    /// first to call DwmGetWindowAttribute to get the window rect without the shadow and uses those offsets
    /// in later calculations.
    /// </summary>
    private static NativeMethods.Rect CalculateBorderOffsets(IntPtr hwnd, int snapGap)
    {
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out NativeMethods.Rect frame) == 0
            && NativeMethods.GetWindowRect(hwnd, out NativeMethods.Rect rect))
        {
            SubRect(ref frame, rect);
            if (snapGap > 0)
            {
                NativeMethods.OffsetRect(out frame, -snapGap, -snapGap);
            }

            return frame;
        }

        NativeMethods.Rect empty = default;
        if (snapGap > 0)
        {
            NativeMethods.OffsetRect(out frame, -snapGap, -snapGap);
        }

        return empty;
    }

    public (int Left, int Top, int Right, int Bottom) SnapResizingWindow(
        int left,
        int top,
        int right,
        int bottom,
        ResizeOperation operation)
    {
        if (!_snappingEnabled)
        {
            return (left, top, right, bottom);
        }

        var positionX = left + _windowBorderOffsets.left;
        var positionY = top + _windowBorderOffsets.top;
        var width = right - left - _windowBorderOffsets.left - _windowBorderOffsets.right;
        var height = bottom - top - _windowBorderOffsets.top - _windowBorderOffsets.bottom;

        // thresholdX and thresholdY will shrink to make sure the dragged window will snap to the closest windows
        var thresholdX = _snappingThreshold;
        var thresholdY = _snappingThreshold;

        var stuckLeft = false;
        var stuckTop = false;
        var stuckRight = false;
        var stuckBottom = false;
        var stickLeft = 0;
        var stickTop = 0;
        var stickRight = 0;
        var stickBottom = 0;

        bool IsLeft(ResizeOperation op) => op is ResizeOperation.ResizeTopLeft or ResizeOperation.ResizeBottomLeft;
        bool IsRight(ResizeOperation op) => op is ResizeOperation.ResizeTopRight or ResizeOperation.ResizeBottomRight;
        bool IsTop(ResizeOperation op) => op is ResizeOperation.ResizeTopLeft or ResizeOperation.ResizeTopRight;
        bool IsBottom(ResizeOperation op) => op is ResizeOperation.ResizeBottomLeft or ResizeOperation.ResizeBottomRight;

        foreach (var snappable in _snappableAreas)
        {
            var rect = snappable.Rect;
            var snapInside = snappable.SnapInside;

            // Check if positionX snaps
            if (IsInRange(positionY, rect.top, rect.bottom, thresholdX)
                || IsInRange(rect.top, positionY, positionY + height, thresholdX))
            {
                var shouldSnapInside = snapInside
                                      || (positionY + height - thresholdX < rect.top)
                                      || (rect.bottom < positionY + thresholdX);
                if (IsLeft(operation)
                    && IsEqual(rect.right, positionX, thresholdX))
                {
                    // The left edge of the dragged window will snap to this window's right edge
                    stuckLeft = true;
                    stickLeft = rect.right;
                    thresholdX = rect.right - positionX;
                }
                else if (shouldSnapInside && IsRight(operation)
                                         && IsEqual(rect.right, positionX + width, thresholdX))
                {
                    // The right edge of the dragged window will snap to this window's right edge
                    stuckRight = true;
                    stickRight = rect.right;
                    thresholdX = rect.right - (positionX + width);
                }
                else if (shouldSnapInside && IsLeft(operation)
                                         && IsEqual(rect.left, positionX, thresholdX))
                {
                    // The left edge of the dragged window will snap to this window's left edge
                    stuckLeft = true;
                    stickLeft = rect.left;
                    thresholdX = rect.left - positionX;
                }
                else if (IsRight(operation)
                         && IsEqual(rect.left, positionX + width, thresholdX))
                {
                    // The right edge of the dragged window will snap to this window's left edge
                    stuckRight = true;
                    stickRight = rect.left;
                    thresholdX = rect.left - (positionX + width);
                }
            }

            // Check if positionY snaps
            if (IsInRange(positionX, rect.left, rect.right, thresholdY)
                || IsInRange(rect.left, positionX, positionX + width, thresholdY))
            {
                var shouldSnapInside = snapInside
                                      || (positionX + width - thresholdY < rect.left)
                                      || (rect.right < positionX + thresholdY);
                if (IsTop(operation)
                    && IsEqual(rect.bottom, positionY, thresholdY))
                {
                    // The top edge of the dragged window will snap to this window's bottom edge
                    stuckTop = true;
                    stickTop = rect.bottom;
                    thresholdY = rect.bottom - positionY;
                }
                else if (shouldSnapInside && IsBottom(operation)
                                         && IsEqual(rect.bottom, positionY + height, thresholdY))
                {
                    // The bottom edge of the dragged window will snap to this window's bottom edge
                    stuckBottom = true;
                    stickBottom = rect.bottom;
                    thresholdY = rect.bottom - (positionY + height);
                }
                else if (shouldSnapInside && IsTop(operation)
                                         && IsEqual(rect.top, positionY, thresholdY))
                {
                    // The top edge of the dragged window will snap to this window's top edge
                    stuckTop = true;
                    stickTop = rect.top;
                    thresholdY = rect.top - positionY;
                }
                else if (IsBottom(operation)
                         && IsEqual(rect.top, positionY + height, thresholdY))
                {
                    // The bottom edge of the dragged window will snap to this window's top edge
                    stuckBottom = true;
                    stickBottom = rect.top;
                    thresholdY = rect.top - (positionY + height);
                }
            }
        }

        if (stuckLeft)
        {
            width = width + positionX - stickLeft + _windowBorderOffsets.left;
            positionX = stickLeft - _windowBorderOffsets.left;
        }

        if (stuckTop)
        {
            height = height + positionY - stickTop + _windowBorderOffsets.top;
            positionY = stickTop - _windowBorderOffsets.top;
        }

        if (stuckRight)
        {
            width = stickRight - positionX + _windowBorderOffsets.right;
        }

        if (stuckBottom)
        {
            height = stickBottom - positionY + _windowBorderOffsets.bottom;
        }

        return (positionX, positionY, positionX + width, positionY + height);
    }

    public (int Left, int Top, int Right, int Bottom) SnapMovingWindow(int left, int top, int right, int bottom)
    {
        if (!_snappingEnabled)
        {
            return (left, top, right, bottom);
        }

        var positionX = left + _windowBorderOffsets.left;
        var positionY = top + _windowBorderOffsets.top;
        var width = right - left - _windowBorderOffsets.left - _windowBorderOffsets.right;
        var height = bottom - top - _windowBorderOffsets.top - _windowBorderOffsets.bottom;

        var thresholdX = _snappingThreshold;
        var thresholdY = _snappingThreshold;

        bool stuckX = false;
        bool stuckY = false;
        var stickX = 0;
        var stickY = 0;

        foreach (var snappable in _snappableAreas)
        {
            var rect = snappable.Rect;
            var snapInside = snappable.SnapInside;

            // Check if positionX snaps
            if (IsInRange(positionY, rect.top, rect.bottom, thresholdX)
                || IsInRange(rect.top, positionY, positionY + height, thresholdX))
            {
                var shouldSnapInside = snapInside
                                      || positionY + height - thresholdX < rect.top
                                      || rect.bottom < positionY + thresholdX;
                if (IsEqual(rect.right, positionX, thresholdX))
                {
                    // The left edge of the dragged window will snap to this window's right edge
                    stuckX = true;
                    stickX = rect.right;
                    thresholdX = rect.right - positionX;
                }
                else if (shouldSnapInside && IsEqual(rect.right, positionX + width, thresholdX))
                {
                    // The right edge of the dragged window will snap to this window's right edge
                    stuckX = true;
                    stickX = rect.right - width;
                    thresholdX = rect.right - (positionX + width);
                }
                else if (shouldSnapInside && IsEqual(rect.left, positionX, thresholdX))
                {
                    // The left edge of the dragged window will snap to this window's left edge
                    stuckX = true;
                    stickX = rect.left;
                    thresholdX = rect.left - positionX;
                }
                else if (IsEqual(rect.left, positionX + width, thresholdX))
                {
                    // The right edge of the dragged window will snap to this window's left edge
                    stuckX = true;
                    stickX = rect.left - width;
                    thresholdX = rect.left - (positionX + width);
                }
            }

            // Check if positionY snaps
            if (IsInRange(positionX, rect.left, rect.right, thresholdY)
                || IsInRange(rect.left, positionX, positionX + width, thresholdY))
            {
                var shouldSnapInside = snapInside || positionX + width - thresholdY < rect.left
                                                 || rect.right < positionX + thresholdY;
                if (IsEqual(rect.bottom, positionY, thresholdY))
                {
                    // The top edge of the dragged window will snap to this window's bottom edge
                    stuckY = true;
                    stickY = rect.bottom;
                    thresholdY = rect.bottom - positionY;
                }
                else if (shouldSnapInside && IsEqual(rect.bottom, positionY + height, thresholdY))
                {
                    // The bottom edge of the dragged window will snap to this window's bottom edge
                    stuckY = true;
                    stickY = rect.bottom - height;
                    thresholdY = rect.bottom - (positionY + height);
                }
                else if (shouldSnapInside && IsEqual(rect.top, positionY, thresholdY))
                {
                    // The top edge of the dragged window will snap to this window's top edge
                    stuckY = true;
                    stickY = rect.top;
                    thresholdY = rect.top - positionY;
                }
                else if (IsEqual(rect.top, positionY + height, thresholdY))
                {
                    // The bottom edge of the dragged window will snap to this window's top edge
                    stuckY = true;
                    stickY = rect.top - height;
                    thresholdY = rect.top - (positionY + height);
                }
            }
        }

        var newLeft = left;
        var newTop = top;
        var newRight = right;
        var newBottom = bottom;

        if (stuckX)
        {
            newLeft = stickX - _windowBorderOffsets.left;
            newRight = newLeft + (right - left);
        }

        if (stuckY)
        {
            newTop = stickY - _windowBorderOffsets.top;
            newBottom = newTop + (bottom - top);
        }

        return (newLeft, newTop, newRight, newBottom);
    }
}
