// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using Common.UI;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PowerToys.Telemetry;
using PowerToys.Interop;
using QuickWindows.Settings;

namespace QuickWindows;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, IDisposable
{
    public ETWTrace EtwTrace { get; } = new();

    private IHost? _host;
    private Mutex? _instanceMutex;
    private bool _disposedValue;

    private CancellationTokenSource NativeThreadCTS { get; set; } = default!;

    private static CancellationToken ExitToken { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            var appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
            }
        }
        catch (CultureNotFoundException ex)
        {
            Logger.LogError("CultureNotFoundException: " + ex.Message);
        }

        NativeThreadCTS = new CancellationTokenSource();
        ExitToken = NativeThreadCTS.Token;

        // allow only one instance of quick windows
        _instanceMutex = new Mutex(true, @"Local\PowerToys_QuickWindows_InstanceMutex", out var createdNew);
        if (!createdNew)
        {
            Logger.LogWarning("There is a QuickWindows instance running. Exiting Quick Windows.");
            _instanceMutex = null;
            Shutdown(0);
            return;
        }

        if (e.Args.Length > 0)
        {
            _ = int.TryParse(e.Args[0], out var powerToysRunnerPid);

            Logger.LogInfo($"Quick Windows started from the PowerToys Runner. Runner pid={powerToysRunnerPid}");
            RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
            {
                Logger.LogInfo("PowerToys Runner exited. Exiting QuickWindows");
                NativeThreadCTS.Cancel();
                Dispatcher.Invoke(Shutdown);
            });
        }

        NativeEventWaiter.WaitForEventLoop(
            Constants.TerminateQuickWindowsSharedEvent(),
            Current.Shutdown,
            Current.Dispatcher,
            ExitToken);

        NativeEventWaiter.WaitForEventLoop(
            Constants.QuickWindowsSendSettingsTelemetryEvent(),
            () => _host!.Services.GetService<IUserSettings>()!.SendSettingsTelemetry(),
            Current.Dispatcher,
            ExitToken);

        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        DependencyInjection.Configure(builder.Services);
        _host = builder.Build();
        _host.RunAsync(ExitToken);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.LogDebug("Exiting Quick Windows.");

        NativeThreadCTS.Cancel();

        // Stop the host synchronously to ensure hooks are uninstalled before the process exits.
        _host?.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();

        _instanceMutex?.ReleaseMutex();

        base.OnExit(e);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        if (disposing)
        {
            _host?.Dispose();
            _instanceMutex?.Dispose();
            EtwTrace.Dispose();
        }

        _disposedValue = true;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
