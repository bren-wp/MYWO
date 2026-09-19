using System.Globalization;
using System.Text;
using MYWO.Desktop.Data;
using MYWO.Desktop.Models;

namespace MYWO.Desktop.Services;

public static class CsvImportService
{
    public static void CreateTemplate(string path)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("vrsta;naziv;sifra;marka;kategorija;jedinica_mjere;cijena_po_jedinici;maloprodajna_cijena;valuta;poseban_oblik_prodaje;naziv_posebnog_oblika_prodaje;sidrena_cijena;barkod;dostupnost;opis_napomena;aktivno");
        sw.WriteLine("proizvod;Primjer proizvoda;ART-001;Primjer brend;Primjer kategorija;kom;24,90;24,90;EUR;ne;;;3850000000000;Dostupno;Opis proizvoda;da");
        sw.WriteLine("usluga;Primjer usluge;;;Usluge;;;49,90;EUR;ne;;;;;Napomena uz uslugu;da");
    }

    public static CsvImportResult Import(long companyId, string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("CSV datoteka ne postoji.", path);
        var result = new CsvImportResult();
        var lines = ReadAllLines(path);
        if (lines.Length == 0) throw new InvalidOperationException("CSV datoteka je prazna.");

        var delimiter = DetectDelimiter(lines[0]);
        var headers = ParseLine(lines[0], delimiter)
            .Select(NormalizeHeader)
            .ToArray();
        var index = headers
            .Select((name, i) => (name, i))
            .Where(x => !string.IsNullOrWhiteSpace(x.name))
            .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);

        if (!index.ContainsKey("naziv") || !index.ContainsKey("maloprodajna_cijena"))
            throw new InvalidOperationException("CSV mora sadržavati stupce 'naziv' i 'maloprodajna_cijena'.");

        var categories = AppDb.Categories(companyId).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var brands = AppDb.Brands(companyId).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var products = AppDb.Products(companyId);
        var services = AppDb.Services(companyId);

        for (var lineNo = 2; lineNo <= lines.Length; lineNo++)
        {
            var raw = lines[lineNo - 1];
            if (string.IsNullOrWhiteSpace(raw)) continue;
            try
            {
                var fields = ParseLine(raw, delimiter);
                string Get(string key)
                    => index.TryGetValue(key, out var i) && i < fields.Count ? fields[i].Trim() : "";

                var type = Get("vrsta").ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(type)) type = "proizvod";
                if (type is "product") type = "proizvod";
                if (type is "service") type = "usluga";

                var name = Get("naziv");
                if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Naziv je prazan.");
                var retail = ParseRequiredDecimal(Get("maloprodajna_cijena"));
                var anchor = ParseOptionalDecimal(Get("sidrena_cijena"));
                var special = ParseBool(Get("poseban_oblik_prodaje"));
                var active = !index.ContainsKey("aktivno") || ParseBool(Get("aktivno"), defaultValue: true);
                var categoryName = Get("kategorija");
                var categoryId = EnsureCategory(companyId, categoryName, type, categories);

                if (type == "usluga")
                {
                    var existing = services.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    var item = existing ?? new ServiceItem { CompanyId = companyId };
                    item.Name = name;
                    item.CategoryId = categoryId;
                    item.RetailPrice = retail;
                    item.SpecialSale = special;
                    item.SpecialSaleName = Get("naziv_posebnog_oblika_prodaje");
                    item.AnchorPrice = anchor;
                    item.Notes = Get("opis_napomena");
                    item.IsActive = active;
                    var savedId = AppDb.SaveService(item);
                    item.Id = savedId;
                    if (existing is null)
                    {
                        result.Inserted++;
                        services.Add(item);
                    }
                    else result.Updated++;
                    continue;
                }

                if (type != "proizvod") throw new InvalidOperationException($"Nepoznata vrsta '{type}'.");

                var code = Get("sifra");
                var barcode = Get("barkod");
                var existingProduct = FindProduct(products, name, code, barcode);
                var product = existingProduct ?? new Product { CompanyId = companyId };
                product.Name = name;
                product.Code = code;
                product.CategoryId = categoryId;
                product.BrandId = EnsureBrand(companyId, Get("marka"), brands);
                product.Unit = string.IsNullOrWhiteSpace(Get("jedinica_mjere")) ? "kom" : Get("jedinica_mjere");
                product.UnitPrice = ParseOptionalDecimal(Get("cijena_po_jedinici"));
                product.RetailPrice = retail;
                product.SpecialSale = special;
                product.SpecialSaleName = Get("naziv_posebnog_oblika_prodaje");
                product.AnchorPrice = anchor;
                product.Barcode = barcode;
                product.Availability = string.IsNullOrWhiteSpace(Get("dostupnost")) ? "Dostupno" : Get("dostupnost");
                product.Description = Get("opis_napomena");
                product.IsActive = active;
                var savedId = AppDb.SaveProduct(product);
                product.Id = savedId;
                if (existingProduct is null)
                {
                    result.Inserted++;
                    products.Add(product);
                }
                else result.Updated++;
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Errors.Add($"Redak {lineNo}: {ex.Message}");
            }
        }

        return result;
    }

    private static Product? FindProduct(List<Product> products, string name, string code, string barcode)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var byCode = products.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (byCode is not null) return byCode;
        }
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var byBarcode = products.FirstOrDefault(x => x.Barcode.Equals(barcode, StringComparison.Ordinal));
            if (byBarcode is not null) return byBarcode;
        }
        return products.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static long? EnsureCategory(
        long companyId,
        string name,
        string type,
        Dictionary<string, Category> categories)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (categories.TryGetValue(name, out var existing)) return existing.Id;
        var category = new Category
        {
            CompanyId = companyId,
            Name = name.Trim(),
            Kind = type == "usluga" ? "service" : "product",
            IsActive = true
        };
        category.Id = AppDb.SaveCategory(category);
        categories[category.Name] = category;
        return category.Id;
    }

    private static long? EnsureBrand(long companyId, string name, Dictionary<string, Brand> brands)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (brands.TryGetValue(name, out var existing)) return existing.Id;
        var brand = new Brand { CompanyId = companyId, Name = name.Trim(), IsActive = true };
        brand.Id = AppDb.SaveBrand(brand);
        brands[brand.Name] = brand;
        return brand.Id;
    }

    private static string[] ReadAllLines(string path)
    {
        try
        {
            return File.ReadAllLines(path, new UTF8Encoding(false, true));
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return File.ReadAllLines(path, Encoding.GetEncoding(1250));
        }
    }

    private static char DetectDelimiter(string header)
    {
        var candidates = new[] { ';', ',', '\t' };
        return candidates.OrderByDescending(c => CountOutsideQuotes(header, c)).First();
    }

    private static int CountOutsideQuotes(string line, char delimiter)
    {
        var quoted = false;
        var count = 0;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                quoted = !quoted;
            }
            else if (!quoted && line[i] == delimiter) count++;
        }
        return count;
    }

    private static List<string> ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (ch == delimiter && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else current.Append(ch);
        }
        if (quoted) throw new InvalidOperationException("Nezatvoren navodnik u CSV retku.");
        fields.Add(current.ToString());
        return fields;
    }

    private static string NormalizeHeader(string value)
    {
        var s = value.Trim().Trim('\uFEFF').ToLowerInvariant()
            .Replace(' ', '_').Replace('-', '_');
        return s switch
        {
            "type" => "vrsta",
            "name" => "naziv",
            "code" => "sifra",
            "brand" => "marka",
            "category" => "kategorija",
            "unit" => "jedinica_mjere",
            "unit_price" => "cijena_po_jedinici",
            "retail_price" or "price" => "maloprodajna_cijena",
            "special_sale" => "poseban_oblik_prodaje",
            "special_sale_name" => "naziv_posebnog_oblika_prodaje",
            "anchor_price" => "sidrena_cijena",
            "barcode" => "barkod",
            "availability" => "dostupnost",
            "description" or "notes" or "note" => "opis_napomena",
            "active" => "aktivno",
            _ => s
        };
    }

    private static decimal ParseRequiredDecimal(string text)
        => ParseOptionalDecimal(text) ?? throw new InvalidOperationException("Maloprodajna cijena je obavezna.");

    private static decimal? ParseOptionalDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().Replace("€", "", StringComparison.Ordinal).Trim();
        var styles = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
        if (decimal.TryParse(text, styles, CultureInfo.GetCultureInfo("hr-HR"), out var hr)) return hr;
        if (decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out var inv)) return inv;
        throw new InvalidOperationException($"Neispravna cijena '{text}'.");
    }

    private static bool ParseBool(string text, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        return text.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "da" or "yes" or "aktivno" or "aktivan" or "aktivna" => true,
            "0" or "false" or "ne" or "no" or "neaktivno" or "neaktivan" or "neaktivna" => false,
            _ => throw new InvalidOperationException($"Neispravna logička vrijednost '{text}'.")
        };
    }
}
