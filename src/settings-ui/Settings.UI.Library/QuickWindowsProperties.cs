// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public class QuickWindowsProperties
{
    private static readonly string[] ExcludedApplicationsDefaults = [
        "Program Manager||Progman",
        "||Shell_TrayWnd",
        "||WindowsDashboard",
        "Notification Centre||Windows.UI.Core.CoreWindow",
        "Start||Windows.UI.Core.CoreWindow",
        "Quick settings||ControlCenterWindow",
        ];

    [JsonConverter(typeof(BoolPropertyJsonConverter))]
    public bool ActivateOnAlt { get; set; } = true;

    [JsonConverter(typeof(BoolPropertyJsonConverter))]
    public bool ActivateOnShift { get; set; } = false;

    [JsonConverter(typeof(BoolPropertyJsonConverter))]
    public bool ActivateOnCtrl { get; set; } = false;

    [JsonConverter(typeof(BoolPropertyJsonConverter))]
    public bool DoNotActivateOnGameMode { get; set; } = true;

    [JsonConverter(typeof(BoolPropertyJsonConverter))]
    public bool TransparentWindowOnMove { get; set; } = true;

    [JsonConverter(typeof(BoolPropertyJsonConverter))]
    public bool ExcludeAppDetection { get; set; } = false;

    public string ExcludedApplications { get; set; } = string.Join('\r', ExcludedApplicationsDefaults);

    public bool RolodexEnabled { get; set; } = true;

    public bool SnappingEnabled { get; set; } = true;

    public int SnappingThreshold { get; set; } = 30;

    public int SnappingPadding { get; set; } = 5;

    public override string ToString() => JsonSerializer.Serialize(this);
}
