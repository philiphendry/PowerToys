// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace QuickWindows.Telemetry;

[EventData]
public class QuickWindowsSettings : EventBase, IEvent
{
    public QuickWindowsSettings()
    {
        EventName = "QuickWindows_Settings";
    }

    public bool ActivateOnAlt { get; set; }

    public bool ActivateOnShift { get; set; }

    public bool ActivateOnCtrl { get; set; }

    public bool DoNotActivateOnGameMode { get; set; }

    public bool TransparentWindowOnMove { get; set; }

    public bool ExcludeAppDetection { get; set; }

    public string ExcludedApplications { get; set; } = default!;

    public int SnappingThreshold { get; set; }

    public int SnappingPadding { get; set; }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
