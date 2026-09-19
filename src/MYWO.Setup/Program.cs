using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace MYWO.Setup;

internal static class Program
{
    private static readonly string Version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.9.4";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MYWO";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var silent = Has(args, "--silent") || Has(args, "/S");
        var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MYWO");
        string? stage = null;

        try
        {
            if (!silent)
            {
                var answer = MessageBox.Show(
                    $"MYWO {Version}\n\nInstalirati aplikaciju za trenutačnog korisnika?\n\nLokacija:\n{target}\n\nPostojeća instalacija, ako postoji, bit će sigurno zamijenjena.",
                    "MYWO Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer != DialogResult.Yes) return 0;
            }

            CloseRunningApp();
            stage = Path.Combine(Path.GetTempPath(), "MYWO-Setup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);

            var zip = Path.Combine(stage, "payload.zip");
            ExtractEmbeddedPayload(zip);
            var unpacked = Path.Combine(stage, "app");
            ZipFile.ExtractToDirectory(zip, unpacked, true);
            ValidatePayload(unpacked);

            ReplaceInstallTree(unpacked, target);
            TryCreateShortcuts(target);
            RegisterUninstall(target);

            if (!silent)
                MessageBox.Show("MYWO je uspješno instaliran i spreman za korištenje.", "MYWO Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);

            var app = Path.Combine(target, "MYWO.exe");
            if (!silent && File.Exists(app))
                Process.Start(new ProcessStartInfo { FileName = app, WorkingDirectory = target, UseShellExecute = true });

            return 0;
        }
        catch (Exception ex)
        {
            if (!silent)
                MessageBox.Show($"Instalacija nije uspjela.\n\n{ex.Message}", "MYWO Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(stage)) TryDeleteDirectory(stage);
        }
    }

    private static void ExtractEmbeddedPayload(string destination)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().SingleOrDefault(x => x.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException("Instalacijski payload nije pronađen.");
        using var input = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Instalacijski payload se ne može otvoriti.");
        using var output = File.Create(destination);
        input.CopyTo(output);
    }

    private static void ValidatePayload(string path)
    {
        foreach (var required in new[] { "MYWO.exe", "MYWO-Update.exe" })
        {
            var file = Path.Combine(path, required);
            if (!File.Exists(file) || new FileInfo(file).Length < 64 * 1024)
                throw new InvalidOperationException($"Instalacijski paket nije valjan: nedostaje {required}.");
        }
    }

    private static void ReplaceInstallTree(string source, string target)
    {
        var parent = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("Odredišna mapa nije valjana.");
        Directory.CreateDirectory(parent);
        var backup = target + ".previous-" + Guid.NewGuid().ToString("N");

        try
        {
            if (Directory.Exists(target)) Directory.Move(target, backup);
            Directory.Move(source, target);
            TryDeleteDirectory(backup);
        }
        catch
        {
            TryDeleteDirectory(target);
            if (Directory.Exists(backup)) Directory.Move(backup, target);
            throw;
        }
    }

    private static void RegisterUninstall(string installDir)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        var app = Path.Combine(installDir, "MYWO.exe");
        var updater = Path.Combine(installDir, "MYWO-Update.exe");
        key.SetValue("DisplayName", "MYWO");
        key.SetValue("DisplayVersion", Version);
        key.SetValue("Publisher", "MYWO");
        key.SetValue("URLInfoAbout", "https://github.com/bren-wp/MYWO");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", app);
        key.SetValue("UninstallString", $"\"{updater}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{updater}\" --uninstall --quiet");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", EstimateSizeKb(installDir), RegistryValueKind.DWord);
    }

    private static int EstimateSizeKb(string path)
    {
        try
        {
            var bytes = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(x => new FileInfo(x).Length);
            return (int)Math.Min(int.MaxValue, Math.Max(1, bytes / 1024));
        }
        catch { return 1; }
    }

    private static void TryCreateShortcuts(string installDir)
    {
        try
        {
            var app = Path.Combine(installDir, "MYWO.exe");
            var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MYWO.lnk");
            var start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "MYWO.lnk");
            CreateShortcut(desktop, app, installDir);
            CreateShortcut(start, app, installDir);
        }
        catch
        {
            // Shortcut creation must never invalidate an otherwise healthy installation.
        }
    }

    private static void CreateShortcut(string shortcutPath, string target, string workingDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Shell nije dostupan.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = target;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = target + ",0";
        shortcut.Description = "MYWO – Upravljanje cjenicima";
        shortcut.Save();
    }

    private static void CloseRunningApp()
    {
        foreach (var processName in new[] { "MYWO", "MYWO-Portable" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(3500)) process.Kill(true);
                }
                catch { }
            }
        }
    }

    private static bool Has(string[] args, string key) => args.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
}
