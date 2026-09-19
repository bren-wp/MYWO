using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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
        if (sender is Window window) FitWindowToCurrentWorkArea(window);
    }

    internal static void FitWindowToCurrentWorkArea(Window window)
    {
        if (window.WindowState != WindowState.Normal) return;

        var work = GetMonitorWorkArea(window);
        const double inset = 12;
        var maxWidth = Math.Max(360, work.Width - inset * 2);
        var maxHeight = Math.Max(320, work.Height - inset * 2);

        if (window.MinWidth > maxWidth) window.MinWidth = Math.Max(320, maxWidth);
        if (window.MinHeight > maxHeight) window.MinHeight = Math.Max(280, maxHeight);

        if (double.IsNaN(window.Width) || window.Width <= 0 || window.Width > maxWidth) window.Width = maxWidth;
        if (double.IsNaN(window.Height) || window.Height <= 0 || window.Height > maxHeight) window.Height = maxHeight;

        // Dialogs stay inside the current monitor's work area; the main shell may still maximize normally.
        if (window is not MYWO.Desktop.MainWindow)
        {
            window.MaxWidth = Math.Min(window.MaxWidth, maxWidth);
            window.MaxHeight = Math.Min(window.MaxHeight, maxHeight);
        }

        var left = double.IsNaN(window.Left) ? work.Left + (work.Width - window.Width) / 2 : window.Left;
        var top = double.IsNaN(window.Top) ? work.Top + (work.Height - window.Height) / 2 : window.Top;
        var maxLeft = Math.Max(work.Left + inset, work.Right - window.Width - inset);
        var maxTop = Math.Max(work.Top + inset, work.Bottom - window.Height - inset);

        window.Left = Math.Clamp(left, work.Left + inset, maxLeft);
        window.Top = Math.Clamp(top, work.Top + inset, maxTop);
    }

    private static Rect GetMonitorWorkArea(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return SystemParameters.WorkArea;

            var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero) return SystemParameters.WorkArea;

            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return SystemParameters.WorkArea;

            var dpi = VisualTreeHelper.GetDpi(window);
            var x = info.Work.Left / dpi.DpiScaleX;
            var y = info.Work.Top / dpi.DpiScaleY;
            var width = (info.Work.Right - info.Work.Left) / dpi.DpiScaleX;
            var height = (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY;
            return new Rect(x, y, width, height);
        }
        catch
        {
            return SystemParameters.WorkArea;
        }
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
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
