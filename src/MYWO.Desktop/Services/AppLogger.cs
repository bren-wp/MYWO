using System.Text;

namespace MYWO.Desktop.Services;

public static class AppLogger
{
    private static readonly object Sync = new();
    private static string LogDirectory => AppPaths.LogDirectory;
    public static string LogPath => Path.Combine(LogDirectory, "mywo.log");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();
                var line = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("O"))
                    .Append(" [").Append(level).Append("] ")
                    .Append(message);
                if (exception is not null)
                    line.AppendLine().Append(exception);
                File.AppendAllText(LogPath, line.AppendLine().ToString(), new UTF8Encoding(false));
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPath)) return;
        var info = new FileInfo(LogPath);
        if (info.Length < 2 * 1024 * 1024) return;

        var archive = Path.Combine(LogDirectory, "mywo-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
        File.Move(LogPath, archive, true);

        var oldLogs = Directory.GetFiles(LogDirectory, "mywo-*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(x => x.CreationTimeUtc)
            .Skip(5);
        foreach (var old in oldLogs)
        {
            try { old.Delete(); } catch { }
        }
    }
}
