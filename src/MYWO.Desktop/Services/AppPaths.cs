namespace MYWO.Desktop.Services;

public static class AppPaths
{
    private static readonly Lazy<bool> PortableModeLazy = new(DetectPortableMode);

    public static bool IsPortable => PortableModeLazy.Value;

    public static string DataRoot => IsPortable
        ? Path.Combine(AppContext.BaseDirectory, "MYWO-Data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MYWO");

    public static string LogDirectory => Path.Combine(DataRoot, "logs");
    public static string ImagesDirectory => Path.Combine(DataRoot, "images");
    public static string BackupsDirectory => Path.Combine(DataRoot, "backups");

    public static string DefaultPublishRoot => IsPortable
        ? Path.Combine(DataRoot, "Objave")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MYWO", "Objave");

    private static bool DetectPortableMode()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Any(x => string.Equals(x, "--portable", StringComparison.OrdinalIgnoreCase))) return true;

        var exe = Environment.ProcessPath ?? args.FirstOrDefault() ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(exe);
        return name.Contains("Portable", StringComparison.OrdinalIgnoreCase);
    }
}
