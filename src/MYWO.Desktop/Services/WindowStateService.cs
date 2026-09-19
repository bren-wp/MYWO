using System.Text.Json;
using System.Windows;

namespace MYWO.Desktop.Services;

public static class WindowStateService
{
    private static string StatePath => Path.Combine(AppPaths.DataRoot, "window-state.json");

    public static void Restore(Window window)
    {
        try
        {
            if (!File.Exists(StatePath)) return;
            var json = File.ReadAllText(StatePath);
            var state = JsonSerializer.Deserialize<WindowStateSnapshot>(json);
            if (state is null) return;
            if (!IsFinite(state.Left) || !IsFinite(state.Top) || !IsFinite(state.Width) || !IsFinite(state.Height)) return;
            if (state.Width < 480 || state.Height < 360 || state.Width > 10000 || state.Height > 10000) return;

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = state.Left;
            window.Top = state.Top;
            window.Width = Math.Max(window.MinWidth, state.Width);
            window.Height = Math.Max(window.MinHeight, state.Height);
            if (state.Maximized) window.WindowState = WindowState.Maximized;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Window placement restore failed.", ex);
        }
    }

    public static void Save(Window window)
    {
        try
        {
            var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
            if (!IsFinite(bounds.Left) || !IsFinite(bounds.Top) || !IsFinite(bounds.Width) || !IsFinite(bounds.Height)) return;
            if (bounds.Width < 480 || bounds.Height < 360) return;

            Directory.CreateDirectory(AppPaths.DataRoot);
            var snapshot = new WindowStateSnapshot
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                Maximized = window.WindowState == WindowState.Maximized
            };
            var temp = StatePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(snapshot));
            File.Move(temp, StatePath, true);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Window placement save failed.", ex);
        }
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private sealed class WindowStateSnapshot
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool Maximized { get; set; }
    }
}
