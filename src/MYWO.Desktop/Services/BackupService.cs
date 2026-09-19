using MYWO.Desktop.Data;

namespace MYWO.Desktop.Services;

public static class BackupService
{
    public static string CreateBackup(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new InvalidOperationException("Odaberite odredište sigurnosne kopije.");

        AppDb.Checkpoint();
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        File.Copy(AppDb.DatabasePath, destinationPath, true);
        return destinationPath;
    }

    public static string RestoreBackup(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Odabrana sigurnosna kopija ne postoji.", sourcePath);

        using (var fs = File.OpenRead(sourcePath))
        {
            Span<byte> header = stackalloc byte[16];
            if (fs.Read(header) != 16 || System.Text.Encoding.ASCII.GetString(header) != "SQLite format 3\0")
                throw new InvalidOperationException("Odabrana datoteka nije valjana SQLite baza.");
        }

        AppDb.Checkpoint();
        var emergency = AppDb.DatabasePath + ".pre-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
        File.Copy(AppDb.DatabasePath, emergency, true);

        // WAL/SHM pripadaju prethodnoj bazi i ne smiju ostati uz obnovljenu kopiju.
        TryDelete(AppDb.DatabasePath + "-wal");
        TryDelete(AppDb.DatabasePath + "-shm");

        File.Copy(sourcePath, AppDb.DatabasePath, true);
        AppDb.Initialize();
        return emergency;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // Ako je pomoćna SQLite datoteka još kratko zaključana, inicijalizacija baze
            // će svejedno otvoriti obnovljenu glavnu bazu nakon checkpointa.
        }
        catch (UnauthorizedAccessException)
        {
            // Ne prikrivamo grešku glavne restore operacije zbog pomoćne datoteke.
        }
    }
}
