// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using QuickWindows.Features;
using QuickWindows.Helpers;
using QuickWindows.Interfaces;
using QuickWindows.Keyboard;
using QuickWindows.Mouse;
using QuickWindows.Settings;

namespace QuickWindows;

public class DependencyInjection
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<ICursorForOperation, CursorForOperation>();
        services.AddSingleton<IMovingWindows, MovingWindows>();
        services.AddSingleton<IResizingWindows, ResizingWindows>();
        services.AddSingleton<ISnappingWindows, SnappingWindows>();
        services.AddSingleton<IRolodexWindows, RolodexWindows>();
        services.AddSingleton<ITransparentWindows, TransparentWindows>();
        services.AddSingleton<IDisabledInGameMode, DisabledInGameMode>();
        services.AddSingleton<IExclusionDetector, ExclusionDetector>();
        services.AddSingleton<IExclusionFilter, ExclusionFilter>();
        services.AddSingleton<IThrottledActionInvoker, ThrottledActionInvoker>();
        services.AddSingleton<IWindowHelpers, WindowHelpers>();
        services.AddSingleton<IUserSettings, UserSettings>();
        services.AddSingleton<IKeyboardMonitor, KeyboardMonitor>();
        services.AddSingleton<IMouseHook, MouseHook>();
        services.AddTransient<IRateLimiter, RateLimiter>();
        services.AddHostedService<QuickWindowsManager>();
    }
}
