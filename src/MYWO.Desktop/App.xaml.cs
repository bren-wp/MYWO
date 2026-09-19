using System.Windows;
using System.Windows.Threading;
using MYWO.Desktop.Data;
using MYWO.Desktop.Services;

namespace MYWO.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            AppDb.Initialize();
            AppLogger.Info("MYWO Desktop started.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Database initialization failed.", ex);
            MessageBox.Show(
                $"Baza podataka nije mogla biti inicijalizirana.\n\n{ex.Message}\n\nLog: {AppLogger.LogPath}",
                "MYWO", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Info($"MYWO Desktop stopped with code {e.ApplicationExitCode}.");
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Unhandled UI exception.", e.Exception);
        MessageBox.Show(
            $"Dogodila se neočekivana pogreška. Detalji su zapisani u log.\n\n{e.Exception.Message}",
            "MYWO", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) AppLogger.Error("Unhandled application exception.", ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }
}
