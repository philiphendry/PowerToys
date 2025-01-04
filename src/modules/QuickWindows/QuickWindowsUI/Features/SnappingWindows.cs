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
    private List<NativeMethods.Rect> _snappableWindows = null!;
    private NativeMethods.Rect _windowBorderOffsets;

    public SnappingWindows(IUserSettings userSettings, IWindowHelpers windowHelpers)
    {
        _windowHelpers = windowHelpers;

        _snappingThreshold = userSettings.SnappingThreshold.Value;
        _snappingPadding = userSettings.SnappingPadding.Value;

        userSettings.SnappingThreshold.PropertyChanged += (_, _) => _snappingThreshold = userSettings.SnappingThreshold.Value;
        userSettings.SnappingPadding.PropertyChanged += (_, _) => _snappingPadding = userSettings.SnappingPadding.Value;

        Logger.LogDebug($"Snapping Threshold: {_snappingThreshold}, Snapping Padding: {_snappingPadding}");
    }

    public void StartSnap(IntPtr targetWindow)
    {
        _snappableWindows = _windowHelpers.GetSnappableWindows(targetWindow);
        if (_snappingPadding > 0)
        {
            _snappableWindows = _snappableWindows.Select(w =>
            {
                NativeMethods.InflateRect(out w, _snappingPadding, _snappingPadding);
                return w;
            }).ToList();
        }

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
        var positionX = left + _windowBorderOffsets.left;
        var positionY = top + _windowBorderOffsets.top;
        var width = right - left - _windowBorderOffsets.left + _windowBorderOffsets.right;
        var height = bottom - top - _windowBorderOffsets.top + _windowBorderOffsets.bottom;

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

        foreach (var window in _snappableWindows)
        {
            var snapInside = false;

            // Check if positionX snaps
            if (IsInRange(positionY, window.top, window.bottom, thresholdX)
                || IsInRange(window.top, positionY, positionY + height, thresholdX))
            {
                var shouldSnapInside = snapInside
                                      || (positionY + height - thresholdX < window.top)
                                      || (window.bottom < positionY + thresholdX);
                if (IsLeft(operation)
                    && IsEqual(window.right, positionX, thresholdX))
                {
                    // The left edge of the dragged window will snap to this window's right edge
                    stuckLeft = true;
                    stickLeft = window.right;
                    thresholdX = window.right - positionX;
                }
                else if (shouldSnapInside && IsRight(operation)
                                         && IsEqual(window.right, positionX + width, thresholdX))
                {
                    // The right edge of the dragged window will snap to this window's right edge
                    stuckRight = true;
                    stickRight = window.right;
                    thresholdX = window.right - (positionX + width);
                }
                else if (shouldSnapInside && IsLeft(operation)
                                         && IsEqual(window.left, positionX, thresholdX))
                {
                    // The left edge of the dragged window will snap to this window's left edge
                    stuckLeft = true;
                    stickLeft = window.left;
                    thresholdX = window.left - positionX;
                }
                else if (IsRight(operation)
                         && IsEqual(window.left, positionX + width, thresholdX))
                {
                    // The right edge of the dragged window will snap to this window's left edge
                    stuckRight = true;
                    stickRight = window.left;
                    thresholdX = window.left - (positionX + width);
                }
            }

            // Check if positionY snaps
            if (IsInRange(positionX, window.left, window.right, thresholdY)
                || IsInRange(window.left, positionX, positionX + width, thresholdY))
            {
                var shouldSnapInside = snapInside
                                      || (positionX + width - thresholdY < window.left)
                                      || (window.right < positionX + thresholdY);
                if (IsTop(operation)
                    && IsEqual(window.bottom, positionY, thresholdY))
                {
                    // The top edge of the dragged window will snap to this window's bottom edge
                    stuckTop = true;
                    stickTop = window.bottom;
                    thresholdY = window.bottom - positionY;
                }
                else if (shouldSnapInside && IsBottom(operation)
                                         && IsEqual(window.bottom, positionY + height, thresholdY))
                {
                    // The bottom edge of the dragged window will snap to this window's bottom edge
                    stuckBottom = true;
                    stickBottom = window.bottom;
                    thresholdY = window.bottom - (positionY + height);
                }
                else if (shouldSnapInside && IsTop(operation)
                                         && IsEqual(window.top, positionY, thresholdY))
                {
                    // The top edge of the dragged window will snap to this window's top edge
                    stuckTop = true;
                    stickTop = window.top;
                    thresholdY = window.top - positionY;
                }
                else if (IsBottom(operation)
                         && IsEqual(window.top, positionY + height, thresholdY))
                {
                    // The bottom edge of the dragged window will snap to this window's top edge
                    stuckBottom = true;
                    stickBottom = window.top;
                    thresholdY = window.top - (positionY + height);
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

        return (positionX, positionX + width, positionY, positionY + height);
    }

    public (int Left, int Top, int Right, int Bottom) SnapMovingWindow(int left, int top, int right, int bottom)
    {
        var positionX = left + _windowBorderOffsets.left;
        var positionY = top + _windowBorderOffsets.top;
        var width = right - left - _windowBorderOffsets.left + _windowBorderOffsets.right;
        var height = bottom - top - _windowBorderOffsets.top + _windowBorderOffsets.bottom;

        var thresholdX = _snappingThreshold;
        var thresholdY = _snappingThreshold;

        bool stuckX = false;
        bool stuckY = false;
        var stickX = 0;
        var stickY = 0;

        var snapInside = false;

        foreach (var window in _snappableWindows)
        {
            // Check if positionX snaps
            if (IsInRange(positionY, window.top, window.bottom, thresholdX)
                || IsInRange(window.top, positionY, positionY + height, thresholdX))
            {
                var shouldSnapInside = snapInside
                                      || positionY + height - thresholdX < window.top
                                      || window.bottom < positionY + thresholdX;
                if (IsEqual(window.right, positionX, thresholdX))
                {
                    // The left edge of the dragged window will snap to this window's right edge
                    stuckX = true;
                    stickX = window.right;
                    thresholdX = window.right - positionX;
                }
                else if (shouldSnapInside && IsEqual(window.right, positionX + width, thresholdX))
                {
                    // The right edge of the dragged window will snap to this window's right edge
                    stuckX = true;
                    stickX = window.right - width;
                    thresholdX = window.right - (positionX + width);
                }
                else if (shouldSnapInside && IsEqual(window.left, positionX, thresholdX))
                {
                    // The left edge of the dragged window will snap to this window's left edge
                    stuckX = true;
                    stickX = window.left;
                    thresholdX = window.left - positionX;
                }
                else if (IsEqual(window.left, positionX + width, thresholdX))
                {
                    // The right edge of the dragged window will snap to this window's left edge
                    stuckX = true;
                    stickX = window.left - width;
                    thresholdX = window.left - (positionX + width);
                }
            }

            // Check if positionY snaps
            if (IsInRange(positionX, window.left, window.right, thresholdY)
                || IsInRange(window.left, positionX, positionX + width, thresholdY))
            {
                var shouldSnapInside = snapInside || positionX + width - thresholdY < window.left
                                                 || window.right < positionX + thresholdY;
                if (IsEqual(window.bottom, positionY, thresholdY))
                {
                    // The top edge of the dragged window will snap to this window's bottom edge
                    stuckY = true;
                    stickY = window.bottom;
                    thresholdY = window.bottom - positionY;
                }
                else if (shouldSnapInside && IsEqual(window.bottom, positionY + height, thresholdY))
                {
                    // The bottom edge of the dragged window will snap to this window's bottom edge
                    stuckY = true;
                    stickY = window.bottom - height;
                    thresholdY = window.bottom - (positionY + height);
                }
                else if (shouldSnapInside && IsEqual(window.top, positionY, thresholdY))
                {
                    // The top edge of the dragged window will snap to this window's top edge
                    stuckY = true;
                    stickY = window.top;
                    thresholdY = window.top - positionY;
                }
                else if (IsEqual(window.top, positionY + height, thresholdY))
                {
                    // The bottom edge of the dragged window will snap to this window's top edge
                    stuckY = true;
                    stickY = window.top - height;
                    thresholdY = window.top - (positionY + height);
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
