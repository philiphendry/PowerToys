#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if QuickWindows is enabled or disabled
    static void EnableQuickWindows(const bool enabled) noexcept;

};
