using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;

namespace MYWO.Updater;

internal static class Program
{
    private const string ProductName = "MYWO";
    private const string DefaultManifestUrl = "https://github.com/bren-wp/MYWO/releases/latest/download/update.json";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MYWO";
    private const long MaxPackageBytes = 512L * 1024 * 1024;

    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            if (Has(args, "--apply")) return await ApplyUpdateAsync(args);
            if (Has(args, "--uninstall-apply")) return await ApplyUninstallAsync(args);
            if (Has(args, "--uninstall")) return BeginUninstall(args);
            return await CheckAndUpdateAsync(args);
        }
        catch (Exception ex)
        {
            if (!Has(args, "--quiet"))
                MessageBox.Show($"MYWO Update nije mogao dovršiti radnju.\n\n{ex.Message}", "MYWO Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static async Task<int> CheckAndUpdateAsync(string[] args)
    {
        var quiet = Has(args, "--quiet");
        var manifestUrl = Value(args, "--manifest") ?? DefaultManifestUrl;
        EnsureHttps(manifestUrl, "manifest");

        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var appExe = Path.Combine(appDir, "MYWO.exe");
        var currentVersion = ReadVersion(appExe) ?? new Version(0, 0, 0);

        using var http = CreateHttpClient();
        var json = await http.GetStringAsync(manifestUrl);
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                       ?? throw new InvalidOperationException("Manifest ažuriranja nije valjan.");
        ValidateManifest(manifest);

        if (!Version.TryParse(manifest.Version, out var remoteVersion))
            throw new InvalidOperationException("Verzija u manifestu nije valjana.");

        if (remoteVersion <= currentVersion)
        {
            if (!quiet)
                MessageBox.Show($"MYWO je već ažuran.\n\nInstalirana verzija: {currentVersion}", "MYWO Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        if (!quiet)
        {
            var result = MessageBox.Show(
                $"Dostupna je nova verzija MYWO-a.\n\nInstalirana: {currentVersion}\nNova: {remoteVersion}\n\nŽelite li sada preuzeti i instalirati ažuriranje?",
                "MYWO Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result != DialogResult.Yes) return 0;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "MYWO-Update", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var package = Path.Combine(tempRoot, "update.zip");
        await DownloadPackageAsync(http, manifest.Url, package);
        VerifySha256(package, manifest.Sha256);

        var tempUpdater = Path.Combine(tempRoot, "MYWO-Update.exe");
        File.Copy(Environment.ProcessPath!, tempUpdater, true);
        Process.Start(new ProcessStartInfo
        {
            FileName = tempUpdater,
            Arguments = $"--apply {Q(package)} --target {Q(appDir)} --version {Q(remoteVersion.ToString())} {(quiet ? "--quiet" : string.Empty)}",
            UseShellExecute = true
        });
        return 0;
    }

    private static async Task DownloadPackageAsync(HttpClient http, string url, string destination)
    {
        EnsureHttps(url, "paket ažuriranja");
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxPackageBytes)
            throw new InvalidOperationException("Paket ažuriranja je neočekivano velik.");

        await using var src = await response.Content.ReadAsStreamAsync();
        await using var dst = File.Create(destination);
        var buffer = new byte[128 * 1024];
        long total = 0;
        int read;
        while ((read = await src.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > MaxPackageBytes) throw new InvalidOperationException("Paket ažuriranja je prevelik.");
            await dst.WriteAsync(buffer.AsMemory(0, read));
        }
    }

    private static async Task<int> ApplyUpdateAsync(string[] args)
    {
        var package = Required(args, "--apply");
        var target = Required(args, "--target");
        var version = Value(args, "--version") ?? "nova";
        var quiet = Has(args, "--quiet");
        await Task.Delay(1200);
        CloseRunningApp();

        var stagingRoot = Path.Combine(Path.GetTempPath(), "MYWO-Stage", Guid.NewGuid().ToString("N"));
        var unpacked = Path.Combine(stagingRoot, "app");
        Directory.CreateDirectory(unpacked);
        ZipFile.ExtractToDirectory(package, unpacked, true);
        ValidatePayload(unpacked);
        ReplaceInstallTree(unpacked, target);
        RegisterUninstall(target, version);
        TryDeleteDirectory(stagingRoot);

        var exe = Path.Combine(target, "MYWO.exe");
        if (!quiet && File.Exists(exe)) Process.Start(new ProcessStartInfo { FileName = exe, WorkingDirectory = target, UseShellExecute = true });
        if (!quiet) MessageBox.Show($"MYWO je uspješno ažuriran na verziju {version}.", "MYWO Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
        ScheduleSelfDelete();
        return 0;
    }

    private static int BeginUninstall(string[] args)
    {
        var quiet = Has(args, "--quiet");
        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var removeData = false;

        if (!quiet)
        {
            if (MessageBox.Show("Želite li deinstalirati MYWO s ovog računala?", "MYWO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return 0;
            removeData = MessageBox.Show(
                "Želite li ukloniti i lokalnu MYWO bazu, logove i korisničke podatke?\n\nOdaberite Ne ako ih želite sačuvati za kasniju instalaciju.",
                "MYWO – korisnički podaci", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "MYWO-Uninstall", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var tempUpdater = Path.Combine(tempRoot, "MYWO-Update.exe");
        File.Copy(Environment.ProcessPath!, tempUpdater, true);
        Process.Start(new ProcessStartInfo
        {
            FileName = tempUpdater,
            Arguments = $"--uninstall-apply {Q(appDir)} --remove-data {(removeData ? "1" : "0")} {(quiet ? "--quiet" : string.Empty)}",
            UseShellExecute = true
        });
        return 0;
    }

    private static async Task<int> ApplyUninstallAsync(string[] args)
    {
        var target = Required(args, "--uninstall-apply");
        var removeData = Value(args, "--remove-data") == "1";
        var quiet = Has(args, "--quiet");
        await Task.Delay(1400);
        CloseRunningApp();
        RemoveShortcuts();
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
            key?.DeleteSubKeyTree("MYWO", false);

        DeleteDirectoryWithRetries(target);
        if (removeData)
        {
            DeleteDirectoryWithRetries(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MYWO"));
            DeleteDirectoryWithRetries(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MYWO"));
        }

        if (!quiet) MessageBox.Show("MYWO je uspješno deinstaliran.", "MYWO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        ScheduleSelfDelete();
        return 0;
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        var updaterVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.9.4";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"MYWO-Updater/{updaterVersion}");
        return http;
    }

    private static void ValidateManifest(UpdateManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Version)) throw new InvalidOperationException("Manifest nema verziju.");
        EnsureHttps(manifest.Url, "paket ažuriranja");
        var hash = (manifest.Sha256 ?? string.Empty).Trim();
        if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidOperationException("SHA-256 u manifestu nije valjan.");
    }

    private static void EnsureHttps(string value, string label)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"URL za {label} mora koristiti HTTPS.");
    }

    private static void ValidatePayload(string path)
    {
        foreach (var required in new[] { "MYWO.exe", "MYWO-Update.exe" })
        {
            var file = Path.Combine(path, required);
            if (!File.Exists(file) || new FileInfo(file).Length < 64 * 1024)
                throw new InvalidOperationException($"Paket ažuriranja nije valjan: nedostaje {required}.");
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

    private static void RegisterUninstall(string installDir, string version)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        var updater = Path.Combine(installDir, "MYWO-Update.exe");
        var exe = Path.Combine(installDir, "MYWO.exe");
        key.SetValue("DisplayName", ProductName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "MYWO");
        key.SetValue("URLInfoAbout", "https://github.com/bren-wp/MYWO");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", exe);
        key.SetValue("UninstallString", $"\"{updater}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{updater}\" --uninstall --quiet");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void CloseRunningApp()
    {
        foreach (var processName in new[] { "MYWO", "MYWO-Portable" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (process.Id == Environment.ProcessId) continue;
                    process.CloseMainWindow();
                    if (!process.WaitForExit(4000)) process.Kill(true);
                }
                catch { }
            }
        }
    }

    private static void RemoveShortcuts()
    {
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MYWO.lnk"));
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "MYWO.lnk"));
    }

    private static void DeleteDirectoryWithRetries(string path)
    {
        if (!Directory.Exists(path)) return;
        Exception? last = null;
        for (var i = 0; i < 5; i++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                Thread.Sleep(350 + i * 250);
            }
        }
        if (Directory.Exists(path)) throw new IOException($"Mapa se ne može ukloniti: {path}", last);
    }

    private static void VerifySha256(string file, string expected)
    {
        using var stream = File.OpenRead(file);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        var normalized = (expected ?? string.Empty).Replace(" ", string.Empty).Trim();
        if (!actual.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SHA-256 provjera paketa nije prošla. Ažuriranje je prekinuto.");
    }

    private static Version? ReadVersion(string exe)
    {
        if (!File.Exists(exe)) return null;
        var value = FileVersionInfo.GetVersionInfo(exe).FileVersion;
        return Version.TryParse(value, out var version) ? version : null;
    }

    private static bool Has(string[] args, string key) => args.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
    private static string? Value(string[] args, string key)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }

    private static string Required(string[] args, string key) => Value(args, key) ?? throw new InvalidOperationException($"Nedostaje argument {key}.");
    private static string Q(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    private static void ScheduleSelfDelete()
    {
        try
        {
            var me = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(me)) return;
            var dir = Path.GetDirectoryName(me)!;
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /c ping 127.0.0.1 -n 3 >nul & del /f /q {Q(me)} & rmdir /q {Q(dir)}",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch { }
    }

    private sealed record UpdateManifest(string Version, string Url, string Sha256);
}
