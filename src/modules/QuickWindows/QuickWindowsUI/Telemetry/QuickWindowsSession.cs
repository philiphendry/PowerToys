// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace QuickWindows.Telemetry
{
    [EventData]
    public class QuickWindowsSession : EventBase, IEvent
    {
        public QuickWindowsSession()
        {
            EventName = "QuickWindows_Session";
        }

        public int Duration { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
