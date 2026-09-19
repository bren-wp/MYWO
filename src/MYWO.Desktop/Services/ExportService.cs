using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using MYWO.Desktop.Data;
using MYWO.Desktop.Models;

namespace MYWO.Desktop.Services;

public sealed record PublishResult(string XmlPath, string CsvPath, int ProductCount, int ServiceCount)
{
    public int TotalCount => ProductCount + ServiceCount;
}

public static class ExportService
{
    public static PublishResult Publish(long companyId, string folder, bool onlyActive = true)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("Odaberite mapu za objavu.");
        folder = Path.GetFullPath(folder.Trim());
        Directory.CreateDirectory(folder);

        var company = AppDb.GetCompany(companyId);
        var products = AppDb.Products(companyId, active: onlyActive ? true : null);
        var services = AppDb.Services(companyId, active: onlyActive ? true : null);
        var categories = AppDb.Categories(companyId).ToDictionary(x => x.Id, x => x.Name);
        var brands = AppDb.Brands(companyId).ToDictionary(x => x.Id, x => x.Name);
        var settings = AppDb.GetCompanySettings(companyId);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        var xmlPath = Path.Combine(folder, $"cjenik-{stamp}.xml");
        var csvPath = Path.Combine(folder, $"cjenik-{stamp}.csv");
        var xmlTemp = xmlPath + ".tmp";
        var csvTemp = csvPath + ".tmp";

        try
        {
            WriteXml(xmlTemp, company, products, services, categories, brands, settings.Currency);
            WriteCsv(csvTemp, products, services, categories, brands, settings.Currency);
            File.Move(xmlTemp, xmlPath, true);
            File.Move(csvTemp, csvPath, true);

            ReplaceStableFile(xmlPath, Path.Combine(folder, "cjenik.xml"));
            ReplaceStableFile(csvPath, Path.Combine(folder, "cjenik.csv"));

            var total = products.Count + services.Count;
            AppDb.AddSnapshot(companyId, "XML", Path.GetFileName(xmlPath), xmlPath, total, Hash(xmlPath));
            AppDb.AddSnapshot(companyId, "CSV", Path.GetFileName(csvPath), csvPath, total, Hash(csvPath));
            return new PublishResult(xmlPath, csvPath, products.Count, services.Count);
        }
        finally
        {
            TryDelete(xmlTemp);
            TryDelete(csvTemp);
        }
    }


    public static PublishResult PublishXml(long companyId, string folder, bool onlyActive = true)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("Odaberite mapu za objavu.");
        folder = Path.GetFullPath(folder.Trim());
        Directory.CreateDirectory(folder);

        var company = AppDb.GetCompany(companyId);
        var products = AppDb.Products(companyId, active: onlyActive ? true : null);
        var services = AppDb.Services(companyId, active: onlyActive ? true : null);
        var categories = AppDb.Categories(companyId).ToDictionary(x => x.Id, x => x.Name);
        var brands = AppDb.Brands(companyId).ToDictionary(x => x.Id, x => x.Name);
        var settings = AppDb.GetCompanySettings(companyId);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        var xmlPath = Path.Combine(folder, $"cjenik-{stamp}.xml");
        var xmlTemp = xmlPath + ".tmp";
        try
        {
            WriteXml(xmlTemp, company, products, services, categories, brands, settings.Currency);
            File.Move(xmlTemp, xmlPath, true);
            ReplaceStableFile(xmlPath, Path.Combine(folder, "cjenik.xml"));
            var total = products.Count + services.Count;
            AppDb.AddSnapshot(companyId, "XML", Path.GetFileName(xmlPath), xmlPath, total, Hash(xmlPath));
            return new PublishResult(xmlPath, "", products.Count, services.Count);
        }
        finally
        {
            TryDelete(xmlTemp);
        }
    }

    public static PublishResult PublishCsv(long companyId, string folder, bool onlyActive = true)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("Odaberite mapu za objavu.");
        folder = Path.GetFullPath(folder.Trim());
        Directory.CreateDirectory(folder);

        var products = AppDb.Products(companyId, active: onlyActive ? true : null);
        var services = AppDb.Services(companyId, active: onlyActive ? true : null);
        var categories = AppDb.Categories(companyId).ToDictionary(x => x.Id, x => x.Name);
        var brands = AppDb.Brands(companyId).ToDictionary(x => x.Id, x => x.Name);
        var settings = AppDb.GetCompanySettings(companyId);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        var csvPath = Path.Combine(folder, $"cjenik-{stamp}.csv");
        var csvTemp = csvPath + ".tmp";
        try
        {
            WriteCsv(csvTemp, products, services, categories, brands, settings.Currency);
            File.Move(csvTemp, csvPath, true);
            ReplaceStableFile(csvPath, Path.Combine(folder, "cjenik.csv"));
            var total = products.Count + services.Count;
            AppDb.AddSnapshot(companyId, "CSV", Path.GetFileName(csvPath), csvPath, total, Hash(csvPath));
            return new PublishResult("", csvPath, products.Count, services.Count);
        }
        finally
        {
            TryDelete(csvTemp);
        }
    }

    public static bool VerifySnapshot(PublicationSnapshot snapshot)
    {
        if (!File.Exists(snapshot.FullPath)) return false;
        return Hash(snapshot.FullPath).Equals(snapshot.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public static string RestoreSnapshot(PublicationSnapshot snapshot, string publishFolder)
    {
        if (!VerifySnapshot(snapshot))
            throw new InvalidOperationException("Odabrana arhivska datoteka nedostaje ili joj SHA-256 kontrolna suma nije valjana.");
        if (string.IsNullOrWhiteSpace(publishFolder))
            publishFolder = Path.GetDirectoryName(snapshot.FullPath) ?? throw new InvalidOperationException("Mapa objave nije dostupna.");
        publishFolder = Path.GetFullPath(publishFolder.Trim());
        Directory.CreateDirectory(publishFolder);
        var stableName = snapshot.Format.Equals("XML", StringComparison.OrdinalIgnoreCase) ? "cjenik.xml"
            : snapshot.Format.Equals("CSV", StringComparison.OrdinalIgnoreCase) ? "cjenik.csv"
            : throw new InvalidOperationException("Nepodržani format arhivske objave.");
        var destination = Path.Combine(publishFolder, stableName);
        ReplaceStableFile(snapshot.FullPath, destination);
        return destination;
    }

    private static void WriteXml(
        string path,
        Company company,
        List<Product> products,
        List<ServiceItem> services,
        Dictionary<long, string> categories,
        Dictionary<long, string> brands,
        string currency)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false),
            NewLineChars = "\n"
        };

        using var w = XmlWriter.Create(path, settings);
        w.WriteStartDocument();
        w.WriteStartElement("cjenik");
        w.WriteAttributeString("verzija", "2.0");
        w.WriteAttributeString("generirano", DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        w.WriteAttributeString("valuta", currency);

        w.WriteStartElement("tvrtka");
        Element(w, "naziv", company.Name);
        Element(w, "oib", company.Oib);
        Element(w, "web", company.Website);
        w.WriteEndElement();

        w.WriteStartElement("proizvodi");
        foreach (var p in products)
        {
            w.WriteStartElement("proizvod");
            w.WriteAttributeString("id", p.Id.ToString(CultureInfo.InvariantCulture));
            Element(w, "naziv", p.Name);
            Element(w, "sifra", p.Code);
            Element(w, "marka", p.BrandId is long brandId && brands.TryGetValue(brandId, out var brand) ? brand : "");
            Element(w, "kategorija", p.CategoryId is long categoryId && categories.TryGetValue(categoryId, out var category) ? category : "");
            Element(w, "jedinicaMjere", p.Unit);
            Element(w, "cijenaPoJedinici", Number(p.UnitPrice));
            Element(w, "maloprodajnaCijena", Number(p.RetailPrice));
            Element(w, "posebanOblikProdaje", p.SpecialSale ? "true" : "false");
            Element(w, "nazivPosebnogOblikaProdaje", p.SpecialSaleName);
            Element(w, "sidrenaCijena", Number(p.AnchorPrice));
            Element(w, "barkod", p.Barcode);
            Element(w, "dostupnost", p.Availability);
            Element(w, "opis", p.Description);
            Element(w, "aktivno", p.IsActive ? "true" : "false");
            Element(w, "zadnjaIzmjena", p.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }
        w.WriteEndElement();

        w.WriteStartElement("usluge");
        foreach (var s in services)
        {
            w.WriteStartElement("usluga");
            w.WriteAttributeString("id", s.Id.ToString(CultureInfo.InvariantCulture));
            Element(w, "naziv", s.Name);
            Element(w, "kategorija", s.CategoryId is long categoryId && categories.TryGetValue(categoryId, out var category) ? category : "");
            Element(w, "maloprodajnaCijena", Number(s.RetailPrice));
            Element(w, "posebanOblikProdaje", s.SpecialSale ? "true" : "false");
            Element(w, "nazivPosebnogOblikaProdaje", s.SpecialSaleName);
            Element(w, "sidrenaCijena", Number(s.AnchorPrice));
            Element(w, "napomena", s.Notes);
            Element(w, "aktivno", s.IsActive ? "true" : "false");
            Element(w, "zadnjaIzmjena", s.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }
        w.WriteEndElement();

        w.WriteEndElement();
        w.WriteEndDocument();
    }

    private static void WriteCsv(
        string path,
        List<Product> products,
        List<ServiceItem> services,
        Dictionary<long, string> categories,
        Dictionary<long, string> brands,
        string currency)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("vrsta;naziv;sifra;marka;kategorija;jedinica_mjere;cijena_po_jedinici;maloprodajna_cijena;valuta;poseban_oblik_prodaje;naziv_posebnog_oblika_prodaje;sidrena_cijena;barkod;dostupnost;opis_napomena;aktivno;zadnja_izmjena");

        foreach (var p in products)
        {
            sw.WriteLine(Row(
                "proizvod", p.Name, p.Code,
                p.BrandId is long brandId && brands.TryGetValue(brandId, out var brand) ? brand : "",
                p.CategoryId is long categoryId && categories.TryGetValue(categoryId, out var category) ? category : "",
                p.Unit, Number(p.UnitPrice), Number(p.RetailPrice), currency,
                p.SpecialSale ? "da" : "ne", p.SpecialSaleName, Number(p.AnchorPrice), p.Barcode,
                p.Availability, p.Description, p.IsActive ? "da" : "ne",
                p.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        }

        foreach (var s in services)
        {
            sw.WriteLine(Row(
                "usluga", s.Name, "", "",
                s.CategoryId is long categoryId && categories.TryGetValue(categoryId, out var category) ? category : "",
                "", "", Number(s.RetailPrice), currency,
                s.SpecialSale ? "da" : "ne", s.SpecialSaleName, Number(s.AnchorPrice), "", "",
                s.Notes, s.IsActive ? "da" : "ne",
                s.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        }
    }

    private static string Row(params string[] values) => string.Join(';', values.Select(Csv));

    private static string Csv(string value)
        => value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static void Element(XmlWriter writer, string name, string value) => writer.WriteElementString(name, value);
    private static string Number(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static void ReplaceStableFile(string source, string destination)
    {
        var temp = destination + ".tmp";
        try
        {
            File.Copy(source, temp, true);
            File.Move(temp, destination, true);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort cleanup */ }
    }
}
