// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using System.Windows;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;

namespace QuickWindows;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, IDisposable
{
    public ETWTrace EtwTrace { get; } = new();

    private IQuickWindowsManager? _quickWindowsManager;
    private Mutex? _instanceMutex;
    private bool _disposedValue;

    private CancellationTokenSource NativeThreadCTS { get; set; } = default!;

    [Export]
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

        Bootstrapper.InitializeContainer(this);
        _quickWindowsManager = Bootstrapper.Container.GetExportedValue<IQuickWindowsManager>();
        _quickWindowsManager!.ActivateHotKey();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _quickWindowsManager?.DeactivateHotKey();
        _quickWindowsManager = null;

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
