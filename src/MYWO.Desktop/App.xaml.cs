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
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(FitWindowToWorkArea));

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

    private static void FitWindowToWorkArea(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || window.WindowState != WindowState.Normal) return;
        var work = SystemParameters.WorkArea;
        var maxWidth = Math.Max(640, work.Width - 24);
        var maxHeight = Math.Max(480, work.Height - 24);

        if (window.MinWidth > maxWidth) window.MinWidth = Math.Max(480, maxWidth);
        if (window.MinHeight > maxHeight) window.MinHeight = Math.Max(360, maxHeight);

        if (double.IsNaN(window.Width) || window.Width <= 0 || window.Width > maxWidth) window.Width = maxWidth;
        if (double.IsNaN(window.Height) || window.Height <= 0 || window.Height > maxHeight) window.Height = maxHeight;

        // Dialogs stay inside the current work area; the main shell may still maximize normally.
        if (window is not MYWO.Desktop.MainWindow)
        {
            window.MaxWidth = Math.Min(window.MaxWidth, maxWidth);
            window.MaxHeight = Math.Min(window.MaxHeight, maxHeight);
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
