// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;

namespace QuickWindows;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Logger.InitializeLogger("\\QuickWindows\\Logs");
        Logger.LogInfo($"Quick Windows started with pid={Environment.ProcessId}");

        if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredQuickWindowsEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        try
        {
            using var application = new App();
            application.InitializeComponent();
            application.Run();
        }
        catch (Exception ex)
        {
            Logger.LogError("Unhandled exception", ex);
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.LogError("Unhandled exception", ex);
        }
        else
        {
            Logger.LogError("Unhandled exception");
        }
    }
}
