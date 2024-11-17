// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public class QuickWindowsProperties
{
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

    public override string ToString() => JsonSerializer.Serialize(this);
}
