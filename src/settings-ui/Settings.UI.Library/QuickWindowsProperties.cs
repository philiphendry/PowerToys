// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class QuickWindowsProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(false, false, true, false, 0x0);

        public QuickWindowsProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
        }

        public HotkeySettings ActivationShortcut { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this);
    }
}
