using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using MYWO.Desktop.Models;
using MYWO.Desktop.Services;

namespace MYWO.Desktop.Data;

public static class AppDb
{
    private static string AppDir => AppPaths.DataRoot;

    public static readonly string DatabasePath = Path.Combine(AppPaths.DataRoot, "mywo.db");
    private static string ConnectionString => $"Data Source={DatabasePath};Cache=Shared;Pooling=True";

    public static void Initialize()
    {
        Directory.CreateDirectory(AppDir);
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
        PRAGMA journal_mode=WAL;
        PRAGMA foreign_keys=ON;

        CREATE TABLE IF NOT EXISTS companies(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            oib TEXT NOT NULL DEFAULT '',
            website TEXT NOT NULL DEFAULT '',
            is_active INTEGER NOT NULL DEFAULT 1,
            created_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS categories(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            parent_id INTEGER NULL,
            name TEXT NOT NULL,
            description TEXT NOT NULL DEFAULT '',
            kind TEXT NOT NULL DEFAULT 'both',
            is_active INTEGER NOT NULL DEFAULT 1,
            updated_at TEXT NOT NULL DEFAULT '',
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE,
            FOREIGN KEY(parent_id) REFERENCES categories(id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS brands(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            name TEXT NOT NULL,
            description TEXT NOT NULL DEFAULT '',
            is_active INTEGER NOT NULL DEFAULT 1,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS products(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            category_id INTEGER NULL,
            brand_id INTEGER NULL,
            name TEXT NOT NULL,
            code TEXT NOT NULL DEFAULT '',
            unit TEXT NOT NULL DEFAULT 'kom',
            unit_price REAL NULL,
            retail_price REAL NOT NULL DEFAULT 0,
            special_sale INTEGER NOT NULL DEFAULT 0,
            special_sale_name TEXT NOT NULL DEFAULT '',
            anchor_price REAL NULL,
            barcode TEXT NOT NULL DEFAULT '',
            availability TEXT NOT NULL DEFAULT 'Dostupno',
            description TEXT NOT NULL DEFAULT '',
            image_path TEXT NOT NULL DEFAULT '',
            is_active INTEGER NOT NULL DEFAULT 1,
            updated_at TEXT NOT NULL,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE,
            FOREIGN KEY(category_id) REFERENCES categories(id) ON DELETE SET NULL,
            FOREIGN KEY(brand_id) REFERENCES brands(id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS services(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            category_id INTEGER NULL,
            name TEXT NOT NULL,
            retail_price REAL NOT NULL DEFAULT 0,
            special_sale INTEGER NOT NULL DEFAULT 0,
            special_sale_name TEXT NOT NULL DEFAULT '',
            anchor_price REAL NULL,
            notes TEXT NOT NULL DEFAULT '',
            is_active INTEGER NOT NULL DEFAULT 1,
            updated_at TEXT NOT NULL,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE,
            FOREIGN KEY(category_id) REFERENCES categories(id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS api_keys(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            name TEXT NOT NULL,
            key_prefix TEXT NOT NULL,
            key_hash TEXT NOT NULL,
            permissions TEXT NOT NULL DEFAULT 'all',
            is_active INTEGER NOT NULL DEFAULT 1,
            created_at TEXT NOT NULL,
            last_used_at TEXT NULL,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS snapshots(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            format TEXT NOT NULL,
            filename TEXT NOT NULL,
            full_path TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL,
            item_count INTEGER NOT NULL DEFAULT 0,
            sha256 TEXT NOT NULL,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS price_history(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            entity_type TEXT NOT NULL,
            entity_id INTEGER NOT NULL,
            entity_name TEXT NOT NULL,
            old_retail_price REAL NULL,
            new_retail_price REAL NULL,
            old_anchor_price REAL NULL,
            new_anchor_price REAL NULL,
            batch_id TEXT NOT NULL DEFAULT '',
            source TEXT NOT NULL DEFAULT 'manual',
            note TEXT NOT NULL DEFAULT '',
            changed_at TEXT NOT NULL,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS price_schedules(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            entity_type TEXT NOT NULL,
            entity_id INTEGER NOT NULL,
            entity_name TEXT NOT NULL,
            new_retail_price REAL NULL,
            new_anchor_price REAL NULL,
            effective_at TEXT NOT NULL,
            status TEXT NOT NULL DEFAULT 'pending',
            note TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL,
            applied_at TEXT NULL,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS audit_log(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            action TEXT NOT NULL,
            entity_type TEXT NOT NULL,
            entity_id INTEGER NULL,
            summary TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS company_settings(
            company_id INTEGER PRIMARY KEY,
            publish_folder TEXT NOT NULL DEFAULT '',
            only_active_publish INTEGER NOT NULL DEFAULT 1,
            api_port INTEGER NOT NULL DEFAULT 8787,
            currency TEXT NOT NULL DEFAULT 'EUR',
            auto_publish_enabled INTEGER NOT NULL DEFAULT 0,
            auto_publish_time TEXT NOT NULL DEFAULT '11:00',
            in_app_notifications INTEGER NOT NULL DEFAULT 1,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS api_request_log(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            api_key_id INTEGER NULL,
            method TEXT NOT NULL,
            path TEXT NOT NULL,
            status_code INTEGER NOT NULL,
            duration_ms INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE,
            FOREIGN KEY(api_key_id) REFERENCES api_keys(id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS notifications(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_id INTEGER NOT NULL,
            kind TEXT NOT NULL DEFAULT 'info',
            title TEXT NOT NULL,
            message TEXT NOT NULL DEFAULT '',
            is_read INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL,
            FOREIGN KEY(company_id) REFERENCES companies(id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS idx_products_company ON products(company_id);
        CREATE INDEX IF NOT EXISTS idx_products_search ON products(company_id,name,code,barcode);
        CREATE INDEX IF NOT EXISTS idx_services_company ON services(company_id);
        CREATE INDEX IF NOT EXISTS idx_categories_company ON categories(company_id);
        CREATE INDEX IF NOT EXISTS idx_brands_company ON brands(company_id);
        CREATE INDEX IF NOT EXISTS idx_api_keys_company ON api_keys(company_id);
        CREATE INDEX IF NOT EXISTS idx_snapshots_company ON snapshots(company_id,created_at DESC);
        CREATE INDEX IF NOT EXISTS idx_price_history_company ON price_history(company_id,changed_at DESC);
        CREATE INDEX IF NOT EXISTS idx_price_history_batch ON price_history(company_id,batch_id);
        CREATE INDEX IF NOT EXISTS idx_price_schedules_due ON price_schedules(company_id,status,effective_at);
        CREATE INDEX IF NOT EXISTS idx_audit_company ON audit_log(company_id,created_at DESC);
        CREATE INDEX IF NOT EXISTS idx_api_requests_company ON api_request_log(company_id,created_at DESC);
        CREATE INDEX IF NOT EXISTS idx_notifications_company ON notifications(company_id,is_read,created_at DESC);
        """;
        cmd.ExecuteNonQuery();

        EnsureColumn(cn, "snapshots", "full_path", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(cn, "categories", "parent_id", "INTEGER NULL");
        EnsureColumn(cn, "categories", "description", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(cn, "categories", "updated_at", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(cn, "brands", "description", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(cn, "products", "description", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(cn, "products", "image_path", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(cn, "services", "notes", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(cn, "price_history", "batch_id", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(cn, "price_history", "source", "TEXT NOT NULL DEFAULT 'manual'");
        EnsureColumn(cn, "price_history", "note", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(cn, "api_keys", "permissions", "TEXT NOT NULL DEFAULT 'all'");
        EnsureColumn(cn, "company_settings", "auto_publish_enabled", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(cn, "company_settings", "auto_publish_time", "TEXT NOT NULL DEFAULT '11:00'");
        EnsureColumn(cn, "company_settings", "in_app_notifications", "INTEGER NOT NULL DEFAULT 1");

        using var count = cn.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM companies";
        if (Convert.ToInt64(count.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
        {
            using var seed = cn.CreateCommand();
            seed.CommandText = """
                INSERT INTO companies(name,oib,website,is_active,created_at)
                VALUES('Moja tvrtka','','',1,$now)
                """;
            seed.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            seed.ExecuteNonQuery();
        }

        using var settings = cn.CreateCommand();
        settings.CommandText = """
            INSERT OR IGNORE INTO company_settings(company_id,publish_folder,only_active_publish,api_port,currency,auto_publish_enabled,auto_publish_time,in_app_notifications)
            SELECT id,'',1,8787,'EUR',0,'11:00',1 FROM companies
            """;
        settings.ExecuteNonQuery();

        using var version = cn.CreateCommand();
        version.CommandText = "PRAGMA user_version=5";
        version.ExecuteNonQuery();
    }

    private static SqliteConnection Open()
    {
        var cn = new SqliteConnection(ConnectionString);
        cn.Open();
        using var pragma = cn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return cn;
    }

    private static void EnsureColumn(SqliteConnection cn, string table, string column, string definition)
    {
        using var check = cn.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        }
        reader.Close();

        using var alter = cn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        alter.ExecuteNonQuery();
    }

    public static void Checkpoint()
    {
        using (var cn = Open())
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
    }

    public static string IntegrityCheck()
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        return Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "unknown";
    }

    public static List<Company> Companies()
    {
        var list = new List<Company>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT id,name,oib,website,is_active FROM companies ORDER BY is_active DESC,name COLLATE NOCASE";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Company
            {
                Id = r.GetInt64(0),
                Name = r.GetString(1),
                Oib = r.GetString(2),
                Website = r.GetString(3),
                IsActive = r.GetInt64(4) == 1
            });
        }
        return list;
    }

    public static Company GetCompany(long companyId)
        => Companies().FirstOrDefault(x => x.Id == companyId)
           ?? throw new InvalidOperationException("Odabrana tvrtka više ne postoji.");

    public static long SaveCompany(Company x)
    {
        if (string.IsNullOrWhiteSpace(x.Name))
            throw new InvalidOperationException("Naziv tvrtke je obavezan.");
        if (!ValidationService.IsValidOib(x.Oib))
            throw new InvalidOperationException("OIB nije valjan.");
        if (!ValidationService.IsValidOptionalWebsite(x.Website))
            throw new InvalidOperationException("Web adresa nije valjana.");

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;

        long id;
        if (x.Id == 0)
        {
            cmd.CommandText = """
                INSERT INTO companies(name,oib,website,is_active,created_at)
                VALUES($n,$o,$w,$a,$d);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O"));
        }
        else
        {
            cmd.CommandText = """
                UPDATE companies SET name=$n,oib=$o,website=$w,is_active=$a WHERE id=$id;
                SELECT $id;
                """;
        }

        cmd.Parameters.AddWithValue("$n", x.Name.Trim());
        cmd.Parameters.AddWithValue("$o", x.Oib.Trim());
        cmd.Parameters.AddWithValue("$w", x.Website.Trim());
        cmd.Parameters.AddWithValue("$a", x.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", x.Id);
        id = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);

        using var settings = cn.CreateCommand();
        settings.Transaction = tx;
        settings.CommandText = """
            INSERT OR IGNORE INTO company_settings(company_id,publish_folder,only_active_publish,api_port,currency)
            VALUES($id,'',1,8787,'EUR')
            """;
        settings.Parameters.AddWithValue("$id", id);
        settings.ExecuteNonQuery();

        AddAudit(cn, tx, id, x.Id == 0 ? "create" : "update", "company", id, x.Name.Trim());
        tx.Commit();
        return id;
    }

    public static DashboardStats Stats(long companyId)
    {
        using var cn = Open();
        int Count(string table, string extra = "")
        {
            if (table is not ("products" or "services" or "categories" or "brands" or "api_keys"))
                throw new InvalidOperationException("Nedopuštena tablica.");
            using var c = cn.CreateCommand();
            c.CommandText = $"SELECT COUNT(*) FROM {table} WHERE company_id=$id {extra}";
            c.Parameters.AddWithValue("$id", companyId);
            return Convert.ToInt32(c.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        DateTime? lastPublished = null;
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT MAX(created_at) FROM snapshots WHERE company_id=$id";
            cmd.Parameters.AddWithValue("$id", companyId);
            var value = cmd.ExecuteScalar();
            if (value is string text && DateTime.TryParse(text, null, DateTimeStyles.RoundtripKind, out var parsed))
                lastPublished = parsed;
        }

        return new DashboardStats
        {
            Products = Count("products"),
            ActiveProducts = Count("products", "AND is_active=1"),
            Services = Count("services"),
            ActiveServices = Count("services", "AND is_active=1"),
            Categories = Count("categories"),
            Brands = Count("brands"),
            ApiKeys = Count("api_keys", "AND is_active=1"),
            LastPublishedAt = lastPublished
        };
    }

    public static List<Category> Categories(long companyId, bool activeOnly = false)
    {
        var list = new List<Category>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT c.id,c.company_id,c.parent_id,COALESCE(p.name,''),c.name,c.description,c.kind,c.is_active,c.updated_at
            FROM categories c
            LEFT JOIN categories p ON p.id=c.parent_id AND p.company_id=c.company_id
            WHERE c.company_id=$c AND ($active=0 OR c.is_active=1)
            ORDER BY COALESCE(p.name,'' ) COLLATE NOCASE,c.name COLLATE NOCASE
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$active", activeOnly ? 1 : 0);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Category
            {
                Id = r.GetInt64(0), CompanyId = r.GetInt64(1), ParentId = r.IsDBNull(2) ? null : r.GetInt64(2),
                ParentName = r.GetString(3), Name = r.GetString(4), Description = r.GetString(5),
                Kind = r.GetString(6), IsActive = r.GetInt64(7) == 1,
                UpdatedAt = string.IsNullOrWhiteSpace(r.GetString(8)) ? DateTime.MinValue : ParseDate(r.GetString(8))
            });
        }
        return list;
    }

    public static long SaveCategory(Category x)
    {
        if (string.IsNullOrWhiteSpace(x.Name)) throw new InvalidOperationException("Naziv kategorije je obavezan.");
        if (x.Kind is not ("both" or "product" or "service")) throw new InvalidOperationException("Vrsta kategorije nije valjana.");
        if (x.ParentId == x.Id && x.Id != 0) throw new InvalidOperationException("Kategorija ne može biti nadređena sama sebi.");

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        EnsureOwned(cn, tx, "categories", x.CompanyId, x.ParentId);
        if (x.ParentId.HasValue && WouldCreateCategoryCycle(cn, tx, x.CompanyId, x.Id, x.ParentId.Value))
            throw new InvalidOperationException("Odabrana nadređena kategorija stvorila bi kružnu hijerarhiju.");

        using var duplicate = cn.CreateCommand();
        duplicate.Transaction = tx;
        duplicate.CommandText = """
            SELECT COUNT(*) FROM categories
            WHERE company_id=$c AND id<>$id AND lower(name)=lower($n) AND IFNULL(parent_id,0)=IFNULL($parent,0)
            """;
        duplicate.Parameters.AddWithValue("$c", x.CompanyId);
        duplicate.Parameters.AddWithValue("$id", x.Id);
        duplicate.Parameters.AddWithValue("$n", x.Name.Trim());
        duplicate.Parameters.AddWithValue("$parent", (object?)x.ParentId ?? DBNull.Value);
        if (Convert.ToInt32(duplicate.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
            throw new InvalidOperationException("Kategorija s tim nazivom već postoji na istoj razini.");

        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = x.Id == 0
            ? "INSERT INTO categories(company_id,parent_id,name,description,kind,is_active,updated_at) VALUES($c,$parent,$n,$d,$k,$a,$u); SELECT last_insert_rowid();"
            : "UPDATE categories SET parent_id=$parent,name=$n,description=$d,kind=$k,is_active=$a,updated_at=$u WHERE id=$id AND company_id=$c; SELECT $id;";
        cmd.Parameters.AddWithValue("$c", x.CompanyId);
        cmd.Parameters.AddWithValue("$parent", (object?)x.ParentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", x.Name.Trim());
        cmd.Parameters.AddWithValue("$d", x.Description.Trim());
        cmd.Parameters.AddWithValue("$k", x.Kind);
        cmd.Parameters.AddWithValue("$a", x.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", x.Id);
        var id = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        AddAudit(cn, tx, x.CompanyId, x.Id == 0 ? "create" : "update", "category", id, x.Name.Trim());
        tx.Commit();
        return id;
    }

    public static List<Brand> Brands(long companyId, bool activeOnly = false)
    {
        var list = new List<Brand>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT id,company_id,name,description,is_active
            FROM brands
            WHERE company_id=$c AND ($active=0 OR is_active=1)
            ORDER BY name COLLATE NOCASE
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$active", activeOnly ? 1 : 0);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Brand
            {
                Id = r.GetInt64(0), CompanyId = r.GetInt64(1),
                Name = r.GetString(2), Description = r.GetString(3), IsActive = r.GetInt64(4) == 1
            });
        }
        return list;
    }

    public static long SaveBrand(Brand x)
    {
        if (string.IsNullOrWhiteSpace(x.Name)) throw new InvalidOperationException("Naziv brenda je obavezan.");

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var duplicate = cn.CreateCommand();
        duplicate.Transaction = tx;
        duplicate.CommandText = "SELECT COUNT(*) FROM brands WHERE company_id=$c AND id<>$id AND lower(name)=lower($n)";
        duplicate.Parameters.AddWithValue("$c", x.CompanyId);
        duplicate.Parameters.AddWithValue("$id", x.Id);
        duplicate.Parameters.AddWithValue("$n", x.Name.Trim());
        if (Convert.ToInt32(duplicate.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
            throw new InvalidOperationException("Brend s tim nazivom već postoji.");

        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = x.Id == 0
            ? "INSERT INTO brands(company_id,name,description,is_active) VALUES($c,$n,$d,$a); SELECT last_insert_rowid();"
            : "UPDATE brands SET name=$n,description=$d,is_active=$a WHERE id=$id AND company_id=$c; SELECT $id;";
        cmd.Parameters.AddWithValue("$c", x.CompanyId);
        cmd.Parameters.AddWithValue("$n", x.Name.Trim());
        cmd.Parameters.AddWithValue("$d", x.Description.Trim());
        cmd.Parameters.AddWithValue("$a", x.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", x.Id);
        var id = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        AddAudit(cn, tx, x.CompanyId, x.Id == 0 ? "create" : "update", "brand", id, x.Name.Trim());
        tx.Commit();
        return id;
    }

    public static List<Product> Products(
        long companyId,
        string search = "",
        bool? active = null,
        string availability = "",
        long? categoryId = null,
        long? brandId = null)
    {
        var list = new List<Product>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        var sql = new StringBuilder("""
            SELECT p.id,p.company_id,p.category_id,COALESCE(c.name,''),p.brand_id,COALESCE(b.name,''),
                   p.name,p.code,p.unit,p.unit_price,p.retail_price,p.special_sale,p.special_sale_name,
                   p.anchor_price,p.barcode,p.availability,p.description,p.image_path,p.is_active,p.updated_at
            FROM products p
            LEFT JOIN categories c ON c.id=p.category_id AND c.company_id=p.company_id
            LEFT JOIN brands b ON b.id=p.brand_id AND b.company_id=p.company_id
            WHERE p.company_id=$c
            """);
        cmd.Parameters.AddWithValue("$c", companyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql.Append(" AND (p.name LIKE $like OR p.code LIKE $like OR p.barcode LIKE $like OR c.name LIKE $like OR b.name LIKE $like)");
            cmd.Parameters.AddWithValue("$like", "%" + search.Trim() + "%");
        }
        if (active.HasValue)
        {
            sql.Append(" AND p.is_active=$active");
            cmd.Parameters.AddWithValue("$active", active.Value ? 1 : 0);
        }
        if (!string.IsNullOrWhiteSpace(availability))
        {
            sql.Append(" AND p.availability=$availability");
            cmd.Parameters.AddWithValue("$availability", availability);
        }
        if (categoryId.HasValue)
        {
            sql.Append(" AND p.category_id=$category");
            cmd.Parameters.AddWithValue("$category", categoryId.Value);
        }
        if (brandId.HasValue)
        {
            sql.Append(" AND p.brand_id=$brand");
            cmd.Parameters.AddWithValue("$brand", brandId.Value);
        }
        sql.Append(" ORDER BY p.name COLLATE NOCASE");
        cmd.CommandText = sql.ToString();

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Product
            {
                Id = r.GetInt64(0),
                CompanyId = r.GetInt64(1),
                CategoryId = r.IsDBNull(2) ? null : r.GetInt64(2),
                CategoryName = r.GetString(3),
                BrandId = r.IsDBNull(4) ? null : r.GetInt64(4),
                BrandName = r.GetString(5),
                Name = r.GetString(6),
                Code = r.GetString(7),
                Unit = r.GetString(8),
                UnitPrice = ReadDecimal(r, 9),
                RetailPrice = ReadDecimal(r, 10) ?? 0m,
                SpecialSale = r.GetInt64(11) == 1,
                SpecialSaleName = r.GetString(12),
                AnchorPrice = ReadDecimal(r, 13),
                Barcode = r.GetString(14),
                Availability = r.GetString(15),
                Description = r.GetString(16),
                ImagePath = r.GetString(17),
                IsActive = r.GetInt64(18) == 1,
                UpdatedAt = ParseDate(r.GetString(19))
            });
        }
        return list;
    }

    public static long SaveProduct(Product x)
    {
        ValidateProduct(x);
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        EnsureOwned(cn, tx, "categories", x.CompanyId, x.CategoryId);
        EnsureOwned(cn, tx, "brands", x.CompanyId, x.BrandId);
        EnsureProductIdentifiersUnique(cn, tx, x);

        decimal? oldRetail = null;
        decimal? oldAnchor = null;
        if (x.Id != 0)
        {
            using var old = cn.CreateCommand();
            old.Transaction = tx;
            old.CommandText = "SELECT retail_price,anchor_price FROM products WHERE id=$id AND company_id=$c";
            old.Parameters.AddWithValue("$id", x.Id);
            old.Parameters.AddWithValue("$c", x.CompanyId);
            using var r = old.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("Proizvod više ne postoji.");
            oldRetail = ReadDecimal(r, 0);
            oldAnchor = ReadDecimal(r, 1);
        }

        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = x.Id == 0
            ? """
              INSERT INTO products(company_id,category_id,brand_id,name,code,unit,unit_price,retail_price,special_sale,special_sale_name,anchor_price,barcode,availability,description,image_path,is_active,updated_at)
              VALUES($c,$cat,$brand,$n,$code,$unit,$up,$rp,$ss,$ssn,$ap,$bc,$av,$desc,$img,$a,$u);
              SELECT last_insert_rowid();
              """
            : """
              UPDATE products SET category_id=$cat,brand_id=$brand,name=$n,code=$code,unit=$unit,unit_price=$up,
                  retail_price=$rp,special_sale=$ss,special_sale_name=$ssn,anchor_price=$ap,barcode=$bc,
                  availability=$av,description=$desc,image_path=$img,is_active=$a,updated_at=$u
              WHERE id=$id AND company_id=$c;
              SELECT $id;
              """;
        cmd.Parameters.AddWithValue("$c", x.CompanyId);
        cmd.Parameters.AddWithValue("$cat", (object?)x.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$brand", (object?)x.BrandId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", x.Name.Trim());
        cmd.Parameters.AddWithValue("$code", x.Code.Trim());
        cmd.Parameters.AddWithValue("$unit", string.IsNullOrWhiteSpace(x.Unit) ? "kom" : x.Unit.Trim());
        cmd.Parameters.AddWithValue("$up", DbNumber(x.UnitPrice));
        cmd.Parameters.AddWithValue("$rp", DbNumber(x.RetailPrice));
        cmd.Parameters.AddWithValue("$ss", x.SpecialSale ? 1 : 0);
        cmd.Parameters.AddWithValue("$ssn", x.SpecialSale ? x.SpecialSaleName.Trim() : "");
        cmd.Parameters.AddWithValue("$ap", DbNumber(x.AnchorPrice));
        cmd.Parameters.AddWithValue("$bc", x.Barcode.Trim());
        cmd.Parameters.AddWithValue("$av", string.IsNullOrWhiteSpace(x.Availability) ? "Dostupno" : x.Availability.Trim());
        cmd.Parameters.AddWithValue("$desc", x.Description.Trim());
        cmd.Parameters.AddWithValue("$img", x.ImagePath.Trim());
        cmd.Parameters.AddWithValue("$a", x.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", x.Id);
        var id = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (x.Id == 0 || oldRetail != x.RetailPrice || oldAnchor != x.AnchorPrice)
        {
            AddPriceHistory(cn, tx, x.CompanyId, "product", id, x.Name.Trim(), oldRetail, x.RetailPrice, oldAnchor, x.AnchorPrice);
        }
        AddAudit(cn, tx, x.CompanyId, x.Id == 0 ? "create" : "update", "product", id, x.Name.Trim());
        tx.Commit();
        return id;
    }

    private static void ValidateProduct(Product x)
    {
        if (string.IsNullOrWhiteSpace(x.Name)) throw new InvalidOperationException("Naziv proizvoda je obavezan.");
        if (x.RetailPrice < 0 || x.UnitPrice < 0 || x.AnchorPrice < 0)
            throw new InvalidOperationException("Cijena ne može biti negativna.");
        if (x.SpecialSale && string.IsNullOrWhiteSpace(x.SpecialSaleName))
            throw new InvalidOperationException("Za poseban oblik prodaje unesite njegov naziv.");
        if (!string.IsNullOrWhiteSpace(x.Barcode) && !x.Barcode.All(char.IsDigit))
            throw new InvalidOperationException("Barkod smije sadržavati samo znamenke.");
    }

    private static void EnsureProductIdentifiersUnique(SqliteConnection cn, SqliteTransaction tx, Product x)
    {
        if (!string.IsNullOrWhiteSpace(x.Code))
        {
            using var code = cn.CreateCommand();
            code.Transaction = tx;
            code.CommandText = "SELECT COUNT(*) FROM products WHERE company_id=$c AND id<>$id AND lower(code)=lower($v)";
            code.Parameters.AddWithValue("$c", x.CompanyId);
            code.Parameters.AddWithValue("$id", x.Id);
            code.Parameters.AddWithValue("$v", x.Code.Trim());
            if (Convert.ToInt32(code.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
                throw new InvalidOperationException("Proizvod s tom šifrom već postoji.");
        }

        if (!string.IsNullOrWhiteSpace(x.Barcode))
        {
            using var barcode = cn.CreateCommand();
            barcode.Transaction = tx;
            barcode.CommandText = "SELECT COUNT(*) FROM products WHERE company_id=$c AND id<>$id AND barcode=$v";
            barcode.Parameters.AddWithValue("$c", x.CompanyId);
            barcode.Parameters.AddWithValue("$id", x.Id);
            barcode.Parameters.AddWithValue("$v", x.Barcode.Trim());
            if (Convert.ToInt32(barcode.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
                throw new InvalidOperationException("Proizvod s tim barkodom već postoji.");
        }
    }

    public static void DeleteProduct(long companyId, long id)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        var name = EntityName(cn, tx, "products", companyId, id);
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM products WHERE id=$id AND company_id=$c";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.ExecuteNonQuery();
        AddAudit(cn, tx, companyId, "delete", "product", id, name);
        tx.Commit();
    }

    public static void SetProductsActive(long companyId, IEnumerable<long> ids, bool active)
        => SetBulkActive("products", "product", companyId, ids, active);

    public static List<ServiceItem> Services(
        long companyId,
        string search = "",
        bool? active = null,
        long? categoryId = null)
    {
        var list = new List<ServiceItem>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        var sql = new StringBuilder("""
            SELECT s.id,s.company_id,s.category_id,COALESCE(c.name,''),s.name,s.retail_price,
                   s.special_sale,s.special_sale_name,s.anchor_price,s.notes,s.is_active,s.updated_at
            FROM services s
            LEFT JOIN categories c ON c.id=s.category_id AND c.company_id=s.company_id
            WHERE s.company_id=$c
            """);
        cmd.Parameters.AddWithValue("$c", companyId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql.Append(" AND (s.name LIKE $like OR c.name LIKE $like)");
            cmd.Parameters.AddWithValue("$like", "%" + search.Trim() + "%");
        }
        if (active.HasValue)
        {
            sql.Append(" AND s.is_active=$active");
            cmd.Parameters.AddWithValue("$active", active.Value ? 1 : 0);
        }
        if (categoryId.HasValue)
        {
            sql.Append(" AND s.category_id=$category");
            cmd.Parameters.AddWithValue("$category", categoryId.Value);
        }
        sql.Append(" ORDER BY s.name COLLATE NOCASE");
        cmd.CommandText = sql.ToString();

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ServiceItem
            {
                Id = r.GetInt64(0), CompanyId = r.GetInt64(1),
                CategoryId = r.IsDBNull(2) ? null : r.GetInt64(2),
                CategoryName = r.GetString(3), Name = r.GetString(4),
                RetailPrice = ReadDecimal(r, 5) ?? 0m,
                SpecialSale = r.GetInt64(6) == 1, SpecialSaleName = r.GetString(7),
                AnchorPrice = ReadDecimal(r, 8), Notes = r.GetString(9), IsActive = r.GetInt64(10) == 1,
                UpdatedAt = ParseDate(r.GetString(11))
            });
        }
        return list;
    }

    public static long SaveService(ServiceItem x)
    {
        if (string.IsNullOrWhiteSpace(x.Name)) throw new InvalidOperationException("Naziv usluge je obavezan.");
        if (x.RetailPrice < 0 || x.AnchorPrice < 0) throw new InvalidOperationException("Cijena ne može biti negativna.");
        if (x.SpecialSale && string.IsNullOrWhiteSpace(x.SpecialSaleName))
            throw new InvalidOperationException("Za poseban oblik prodaje unesite njegov naziv.");

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        EnsureOwned(cn, tx, "categories", x.CompanyId, x.CategoryId);

        decimal? oldRetail = null;
        decimal? oldAnchor = null;
        if (x.Id != 0)
        {
            using var old = cn.CreateCommand();
            old.Transaction = tx;
            old.CommandText = "SELECT retail_price,anchor_price FROM services WHERE id=$id AND company_id=$c";
            old.Parameters.AddWithValue("$id", x.Id);
            old.Parameters.AddWithValue("$c", x.CompanyId);
            using var r = old.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("Usluga više ne postoji.");
            oldRetail = ReadDecimal(r, 0);
            oldAnchor = ReadDecimal(r, 1);
        }

        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = x.Id == 0
            ? """
              INSERT INTO services(company_id,category_id,name,retail_price,special_sale,special_sale_name,anchor_price,notes,is_active,updated_at)
              VALUES($c,$cat,$n,$rp,$ss,$ssn,$ap,$notes,$a,$u);
              SELECT last_insert_rowid();
              """
            : """
              UPDATE services SET category_id=$cat,name=$n,retail_price=$rp,special_sale=$ss,
                  special_sale_name=$ssn,anchor_price=$ap,notes=$notes,is_active=$a,updated_at=$u
              WHERE id=$id AND company_id=$c;
              SELECT $id;
              """;
        cmd.Parameters.AddWithValue("$c", x.CompanyId);
        cmd.Parameters.AddWithValue("$cat", (object?)x.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", x.Name.Trim());
        cmd.Parameters.AddWithValue("$rp", DbNumber(x.RetailPrice));
        cmd.Parameters.AddWithValue("$ss", x.SpecialSale ? 1 : 0);
        cmd.Parameters.AddWithValue("$ssn", x.SpecialSale ? x.SpecialSaleName.Trim() : "");
        cmd.Parameters.AddWithValue("$ap", DbNumber(x.AnchorPrice));
        cmd.Parameters.AddWithValue("$notes", x.Notes.Trim());
        cmd.Parameters.AddWithValue("$a", x.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", x.Id);
        var id = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (x.Id == 0 || oldRetail != x.RetailPrice || oldAnchor != x.AnchorPrice)
            AddPriceHistory(cn, tx, x.CompanyId, "service", id, x.Name.Trim(), oldRetail, x.RetailPrice, oldAnchor, x.AnchorPrice);
        AddAudit(cn, tx, x.CompanyId, x.Id == 0 ? "create" : "update", "service", id, x.Name.Trim());
        tx.Commit();
        return id;
    }

    public static void DeleteService(long companyId, long id)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        var name = EntityName(cn, tx, "services", companyId, id);
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM services WHERE id=$id AND company_id=$c";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.ExecuteNonQuery();
        AddAudit(cn, tx, companyId, "delete", "service", id, name);
        tx.Commit();
    }

    public static void SetServicesActive(long companyId, IEnumerable<long> ids, bool active)
        => SetBulkActive("services", "service", companyId, ids, active);

    public static string BulkUpdatePrices(
        long companyId,
        string entityType,
        IEnumerable<long> ids,
        string target,
        string mode,
        decimal value,
        decimal rounding,
        string note = "")
    {
        var table = entityType switch { "product" => "products", "service" => "services", _ => throw new InvalidOperationException("Nepodržana vrsta stavke.") };
        if (target is not ("retail" or "anchor" or "both")) throw new InvalidOperationException("Nepodržano polje cijene.");
        if (mode is not ("percent" or "fixed" or "set")) throw new InvalidOperationException("Nepodržan način promjene cijene.");
        if (rounding <= 0) rounding = 0.01m;
        var values = ids.Distinct().ToArray();
        if (values.Length == 0) throw new InvalidOperationException("Odaberite barem jednu stavku.");
        var batchId = Guid.NewGuid().ToString("N");
        var changed = 0;

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        foreach (var id in values)
        {
            using var read = cn.CreateCommand();
            read.Transaction = tx;
            read.CommandText = $"SELECT name,retail_price,anchor_price FROM {table} WHERE id=$id AND company_id=$c";
            read.Parameters.AddWithValue("$id", id);
            read.Parameters.AddWithValue("$c", companyId);
            using var r = read.ExecuteReader();
            if (!r.Read()) continue;
            var name = r.GetString(0);
            var oldRetail = ReadDecimal(r, 1);
            var oldAnchor = ReadDecimal(r, 2);
            r.Close();

            var newRetail = oldRetail;
            var newAnchor = oldAnchor;
            if (target is "retail" or "both") newRetail = CalculatePrice(oldRetail ?? 0m, mode, value, rounding);
            if (target is "anchor" or "both")
            {
                if (mode == "set") newAnchor = CalculatePrice(oldAnchor ?? 0m, mode, value, rounding);
                else if (oldAnchor.HasValue) newAnchor = CalculatePrice(oldAnchor.Value, mode, value, rounding);
            }
            if ((newRetail.HasValue && newRetail.Value < 0) || (newAnchor.HasValue && newAnchor.Value < 0)) throw new InvalidOperationException($"Promjena bi stvorila negativnu cijenu za '{name}'.");
            if (newRetail == oldRetail && newAnchor == oldAnchor) continue;

            using var update = cn.CreateCommand();
            update.Transaction = tx;
            update.CommandText = $"UPDATE {table} SET retail_price=$r,anchor_price=$a,updated_at=$u WHERE id=$id AND company_id=$c";
            update.Parameters.AddWithValue("$r", DbNumber(newRetail));
            update.Parameters.AddWithValue("$a", DbNumber(newAnchor));
            update.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$id", id);
            update.Parameters.AddWithValue("$c", companyId);
            update.ExecuteNonQuery();
            AddPriceHistory(cn, tx, companyId, entityType, id, name, oldRetail, newRetail, oldAnchor, newAnchor, batchId, "bulk", note);
            changed++;
        }

        AddAudit(cn, tx, companyId, "bulk_price", entityType, null, $"{changed} stavki • {DescribePriceOperation(target, mode, value)}" + (string.IsNullOrWhiteSpace(note) ? "" : $" • {note.Trim()}"));
        tx.Commit();
        return batchId;
    }

    public static int SchedulePriceChanges(
        long companyId,
        string entityType,
        IEnumerable<long> ids,
        string target,
        string mode,
        decimal value,
        decimal rounding,
        DateTime effectiveAtLocal,
        string note = "")
    {
        if (effectiveAtLocal <= DateTime.Now.AddSeconds(5)) throw new InvalidOperationException("Vrijeme planirane promjene mora biti u budućnosti.");
        var table = entityType switch { "product" => "products", "service" => "services", _ => throw new InvalidOperationException("Nepodržana vrsta stavke.") };
        if (target is not ("retail" or "anchor" or "both")) throw new InvalidOperationException("Nepodržano polje cijene.");
        if (mode is not ("percent" or "fixed" or "set")) throw new InvalidOperationException("Nepodržan način promjene cijene.");
        if (rounding <= 0) rounding = 0.01m;
        var values = ids.Distinct().ToArray();
        if (values.Length == 0) throw new InvalidOperationException("Odaberite barem jednu stavku.");
        var effectiveUtc = effectiveAtLocal.Kind == DateTimeKind.Utc ? effectiveAtLocal : effectiveAtLocal.ToUniversalTime();
        var created = 0;

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        foreach (var id in values)
        {
            using var read = cn.CreateCommand();
            read.Transaction = tx;
            read.CommandText = $"SELECT name,retail_price,anchor_price FROM {table} WHERE id=$id AND company_id=$c";
            read.Parameters.AddWithValue("$id", id);
            read.Parameters.AddWithValue("$c", companyId);
            using var r = read.ExecuteReader();
            if (!r.Read()) continue;
            var name = r.GetString(0);
            var retail = ReadDecimal(r, 1) ?? 0m;
            var anchor = ReadDecimal(r, 2);
            r.Close();

            decimal? newRetail = retail;
            decimal? newAnchor = anchor;
            if (target is "retail" or "both") newRetail = CalculatePrice(retail, mode, value, rounding);
            if (target is "anchor" or "both")
            {
                if (mode == "set") newAnchor = CalculatePrice(anchor ?? 0m, mode, value, rounding);
                else if (anchor.HasValue) newAnchor = CalculatePrice(anchor.Value, mode, value, rounding);
            }
            if ((newRetail.HasValue && newRetail.Value < 0) || (newAnchor.HasValue && newAnchor.Value < 0)) throw new InvalidOperationException($"Planirana promjena bi stvorila negativnu cijenu za '{name}'.");

            using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO price_schedules(company_id,entity_type,entity_id,entity_name,new_retail_price,new_anchor_price,effective_at,status,note,created_at)
                VALUES($c,$t,$id,$n,$r,$a,$e,'pending',$note,$created)
                """;
            cmd.Parameters.AddWithValue("$c", companyId);
            cmd.Parameters.AddWithValue("$t", entityType);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$r", DbNumber(newRetail));
            cmd.Parameters.AddWithValue("$a", DbNumber(newAnchor));
            cmd.Parameters.AddWithValue("$e", effectiveUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$note", note?.Trim() ?? "");
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
            created++;
        }
        AddAudit(cn, tx, companyId, "schedule_price", entityType, null, $"{created} stavki • {effectiveAtLocal:dd.MM.yyyy HH:mm} • {DescribePriceOperation(target, mode, value)}");
        tx.Commit();
        return created;
    }

    public static List<PriceSchedule> PriceSchedules(long companyId, string status = "pending", int limit = 500)
    {
        var list = new List<PriceSchedule>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT id,company_id,entity_type,entity_id,entity_name,new_retail_price,new_anchor_price,effective_at,status,note,created_at,applied_at
            FROM price_schedules
            WHERE company_id=$c AND ($status='' OR status=$status)
            ORDER BY CASE WHEN status='pending' THEN 0 ELSE 1 END,effective_at ASC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$status", status ?? "");
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new PriceSchedule
            {
                Id = r.GetInt64(0), CompanyId = r.GetInt64(1), EntityType = r.GetString(2), EntityId = r.GetInt64(3),
                EntityName = r.GetString(4), NewRetailPrice = ReadDecimal(r, 5), NewAnchorPrice = ReadDecimal(r, 6),
                EffectiveAt = ParseDate(r.GetString(7)).ToLocalTime(), Status = r.GetString(8), Note = r.GetString(9),
                CreatedAt = ParseDate(r.GetString(10)).ToLocalTime(), AppliedAt = r.IsDBNull(11) ? null : ParseDate(r.GetString(11)).ToLocalTime()
            });
        }
        return list;
    }

    public static void CancelPriceSchedule(long companyId, long scheduleId)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var read = cn.CreateCommand();
        read.Transaction = tx;
        read.CommandText = "SELECT entity_name,status FROM price_schedules WHERE id=$id AND company_id=$c";
        read.Parameters.AddWithValue("$id", scheduleId);
        read.Parameters.AddWithValue("$c", companyId);
        using var r = read.ExecuteReader();
        if (!r.Read()) throw new InvalidOperationException("Planirana promjena više ne postoji.");
        var name = r.GetString(0);
        var status = r.GetString(1);
        r.Close();
        if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Samo promjena na čekanju može se otkazati.");
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE price_schedules SET status='cancelled' WHERE id=$id AND company_id=$c AND status='pending'";
        cmd.Parameters.AddWithValue("$id", scheduleId);
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.ExecuteNonQuery();
        AddAudit(cn, tx, companyId, "cancel_price_schedule", "price_schedule", scheduleId, name);
        tx.Commit();
    }

    public static int ApplyDuePriceSchedules(long companyId)
    {
        var applied = 0;
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var due = cn.CreateCommand();
        due.Transaction = tx;
        due.CommandText = """
            SELECT id,entity_type,entity_id,entity_name,new_retail_price,new_anchor_price,note
            FROM price_schedules
            WHERE company_id=$c AND status='pending' AND effective_at <= $now
            ORDER BY effective_at,id
            """;
        due.Parameters.AddWithValue("$c", companyId);
        due.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        var rows = new List<(long Id,string Type,long EntityId,string Name,decimal? Retail,decimal? Anchor,string Note)>();
        using (var r = due.ExecuteReader())
        {
            while (r.Read()) rows.Add((r.GetInt64(0),r.GetString(1),r.GetInt64(2),r.GetString(3),ReadDecimal(r,4),ReadDecimal(r,5),r.GetString(6)));
        }

        foreach (var item in rows)
        {
            var table = item.Type == "product" ? "products" : item.Type == "service" ? "services" : "";
            if (table.Length == 0) continue;
            using var current = cn.CreateCommand();
            current.Transaction = tx;
            current.CommandText = $"SELECT retail_price,anchor_price FROM {table} WHERE id=$id AND company_id=$c";
            current.Parameters.AddWithValue("$id", item.EntityId);
            current.Parameters.AddWithValue("$c", companyId);
            using var rr = current.ExecuteReader();
            if (!rr.Read())
            {
                rr.Close();
                using var fail = cn.CreateCommand();
                fail.Transaction = tx;
                fail.CommandText = "UPDATE price_schedules SET status='failed',applied_at=$at WHERE id=$id";
                fail.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
                fail.Parameters.AddWithValue("$id", item.Id);
                fail.ExecuteNonQuery();
                continue;
            }
            var oldRetail = ReadDecimal(rr, 0);
            var oldAnchor = ReadDecimal(rr, 1);
            rr.Close();
            using var update = cn.CreateCommand();
            update.Transaction = tx;
            update.CommandText = $"UPDATE {table} SET retail_price=$r,anchor_price=$a,updated_at=$u WHERE id=$id AND company_id=$c";
            update.Parameters.AddWithValue("$r", DbNumber(item.Retail));
            update.Parameters.AddWithValue("$a", DbNumber(item.Anchor));
            update.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$id", item.EntityId);
            update.Parameters.AddWithValue("$c", companyId);
            update.ExecuteNonQuery();
            AddPriceHistory(cn, tx, companyId, item.Type, item.EntityId, item.Name, oldRetail, item.Retail, oldAnchor, item.Anchor, $"schedule-{item.Id}", "scheduled", item.Note);
            using var mark = cn.CreateCommand();
            mark.Transaction = tx;
            mark.CommandText = "UPDATE price_schedules SET status='applied',applied_at=$at WHERE id=$id";
            mark.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
            mark.Parameters.AddWithValue("$id", item.Id);
            mark.ExecuteNonQuery();
            AddAudit(cn, tx, companyId, "apply_scheduled_price", item.Type, item.EntityId, item.Name);
            applied++;
        }
        tx.Commit();
        return applied;
    }

    public static void UndoPriceHistory(long companyId, long historyId)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var read = cn.CreateCommand();
        read.Transaction = tx;
        read.CommandText = """
            SELECT entity_type,entity_id,entity_name,old_retail_price,new_retail_price,old_anchor_price,new_anchor_price
            FROM price_history WHERE id=$id AND company_id=$c
            """;
        read.Parameters.AddWithValue("$id", historyId);
        read.Parameters.AddWithValue("$c", companyId);
        using var r = read.ExecuteReader();
        if (!r.Read()) throw new InvalidOperationException("Promjena cijene više ne postoji.");
        var type = r.GetString(0);
        var entityId = r.GetInt64(1);
        var name = r.GetString(2);
        var oldRetail = ReadDecimal(r, 3);
        var newRetail = ReadDecimal(r, 4);
        var oldAnchor = ReadDecimal(r, 5);
        var newAnchor = ReadDecimal(r, 6);
        r.Close();
        var table = type == "product" ? "products" : type == "service" ? "services" : throw new InvalidOperationException("Nepodržana vrsta stavke.");
        using var current = cn.CreateCommand();
        current.Transaction = tx;
        current.CommandText = $"SELECT retail_price,anchor_price FROM {table} WHERE id=$id AND company_id=$c";
        current.Parameters.AddWithValue("$id", entityId);
        current.Parameters.AddWithValue("$c", companyId);
        using var cr = current.ExecuteReader();
        if (!cr.Read()) throw new InvalidOperationException("Stavka više ne postoji.");
        var currentRetail = ReadDecimal(cr, 0);
        var currentAnchor = ReadDecimal(cr, 1);
        cr.Close();
        if (currentRetail != newRetail || currentAnchor != newAnchor)
            throw new InvalidOperationException("Stavka je nakon ove promjene ponovno uređena. Povrat je zaustavljen kako noviji podaci ne bi bili prepisani.");
        using var update = cn.CreateCommand();
        update.Transaction = tx;
        update.CommandText = $"UPDATE {table} SET retail_price=$r,anchor_price=$a,updated_at=$u WHERE id=$id AND company_id=$c";
        update.Parameters.AddWithValue("$r", DbNumber(oldRetail));
        update.Parameters.AddWithValue("$a", DbNumber(oldAnchor));
        update.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$id", entityId);
        update.Parameters.AddWithValue("$c", companyId);
        update.ExecuteNonQuery();
        AddPriceHistory(cn, tx, companyId, type, entityId, name, newRetail, oldRetail, newAnchor, oldAnchor, $"undo-{historyId}", "undo", $"Povrat promjene #{historyId}");
        AddAudit(cn, tx, companyId, "undo_price", type, entityId, $"{name} • povrat promjene #{historyId}");
        tx.Commit();
    }

    private static decimal CalculatePrice(decimal current, string mode, decimal value, decimal rounding)
    {
        var result = mode switch
        {
            "percent" => current * (1m + value / 100m),
            "fixed" => current + value,
            "set" => value,
            _ => current
        };
        if (rounding <= 0) rounding = 0.01m;
        return Math.Round(result / rounding, 0, MidpointRounding.AwayFromZero) * rounding;
    }

    private static string DescribePriceOperation(string target, string mode, decimal value)
    {
        var field = target switch { "retail" => "MPC", "anchor" => "sidrena", _ => "MPC + sidrena" };
        var op = mode switch { "percent" => $"{value:+0.##;-0.##;0}%", "fixed" => $"{value:+0.00;-0.00;0.00} EUR", _ => $"postavi {value:0.00} EUR" };
        return $"{field}: {op}";
    }

    private static void SetBulkActive(string table, string entityType, long companyId, IEnumerable<long> ids, bool active)
    {
        if (table is not ("products" or "services")) throw new InvalidOperationException("Nedopuštena tablica.");
        var values = ids.Distinct().ToArray();
        if (values.Length == 0) return;

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        foreach (var id in values)
        {
            using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"UPDATE {table} SET is_active=$a,updated_at=$u WHERE id=$id AND company_id=$c";
            cmd.Parameters.AddWithValue("$a", active ? 1 : 0);
            cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$c", companyId);
            cmd.ExecuteNonQuery();
        }
        AddAudit(cn, tx, companyId, active ? "bulk_activate" : "bulk_deactivate", entityType, null, $"{values.Length} stavki");
        tx.Commit();
    }

    public static List<ApiKeyRecord> ApiKeys(long companyId)
    {
        var list = new List<ApiKeyRecord>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT k.id,k.company_id,k.name,k.key_prefix,k.permissions,k.is_active,k.created_at,k.last_used_at,
                   COUNT(l.id),SUM(CASE WHEN l.status_code >= 400 THEN 1 ELSE 0 END)
            FROM api_keys k
            LEFT JOIN api_request_log l ON l.api_key_id=k.id
            WHERE k.company_id=$c
            GROUP BY k.id,k.company_id,k.name,k.key_prefix,k.permissions,k.is_active,k.created_at,k.last_used_at
            ORDER BY k.created_at DESC
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ApiKeyRecord
            {
                Id = r.GetInt64(0), CompanyId = r.GetInt64(1), Name = r.GetString(2), Prefix = r.GetString(3),
                Permissions = r.GetString(4), IsActive = r.GetInt64(5) == 1, CreatedAt = ParseDate(r.GetString(6)),
                LastUsedAt = r.IsDBNull(7) ? null : ParseDate(r.GetString(7)),
                RequestCount = r.GetInt64(8), ErrorCount = r.IsDBNull(9) ? 0 : r.GetInt64(9)
            });
        }
        return list;
    }

    public static void InsertApiKey(long companyId, string name, string prefix, string hash, string permissions)
    {
        var normalized = NormalizePermissions(permissions);
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO api_keys(company_id,name,key_prefix,key_hash,permissions,is_active,created_at)
            VALUES($c,$n,$p,$h,$permissions,1,$d)
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$p", prefix);
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$permissions", normalized);
        cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
        AddAudit(cn, tx, companyId, "create", "api_key", null, $"{name} [{normalized}]");
        tx.Commit();
    }

    public static void RevokeApiKey(long companyId, long id)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE api_keys SET is_active=0 WHERE id=$id AND company_id=$c";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.ExecuteNonQuery();
        AddAudit(cn, tx, companyId, "revoke", "api_key", id, "API ključ opozvan");
        tx.Commit();
    }

    public static bool AuthorizeApiKey(long companyId, string hash, string permission, out long apiKeyId)
    {
        apiKeyId = 0;
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT id,permissions FROM api_keys WHERE company_id=$c AND key_hash=$h AND is_active=1 LIMIT 1";
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$h", hash);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return false;
        apiKeyId = r.GetInt64(0);
        var permissions = r.GetString(1);
        if (!PermissionAllows(permissions, permission)) return false;
        r.Close();

        using var update = cn.CreateCommand();
        update.CommandText = "UPDATE api_keys SET last_used_at=$d WHERE id=$id";
        update.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$id", apiKeyId);
        update.ExecuteNonQuery();
        return true;
    }

    public static bool VerifyApiKey(long companyId, string hash)
        => AuthorizeApiKey(companyId, hash, "all", out _);

    public static void LogApiRequest(long companyId, long? apiKeyId, string method, string path, int statusCode, long durationMs)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO api_request_log(company_id,api_key_id,method,path,status_code,duration_ms,created_at)
            VALUES($c,$k,$m,$p,$s,$d,$at)
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$k", (object?)apiKeyId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$m", method);
        cmd.Parameters.AddWithValue("$p", path);
        cmd.Parameters.AddWithValue("$s", statusCode);
        cmd.Parameters.AddWithValue("$d", Math.Max(0, durationMs));
        cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();

        using var prune = cn.CreateCommand();
        prune.CommandText = "DELETE FROM api_request_log WHERE company_id=$c AND created_at < $cutoff";
        prune.Parameters.AddWithValue("$c", companyId);
        prune.Parameters.AddWithValue("$cutoff", DateTime.UtcNow.AddDays(-90).ToString("O"));
        prune.ExecuteNonQuery();
    }

    public static ApiUsageStats ApiUsage(long companyId, int days = 30)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*),SUM(CASE WHEN status_code BETWEEN 200 AND 399 THEN 1 ELSE 0 END)
            FROM api_request_log WHERE company_id=$c AND created_at >= $from
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$from", DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 3650)).ToString("O"));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return new ApiUsageStats();
        return new ApiUsageStats
        {
            RequestCount = r.GetInt64(0),
            SuccessfulRequestCount = r.IsDBNull(1) ? 0 : r.GetInt64(1)
        };
    }

    public static void AddSnapshot(long companyId, string format, string filename, string fullPath, int count, string hash)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO snapshots(company_id,format,filename,full_path,created_at,item_count,sha256)
            VALUES($c,$f,$n,$p,$d,$i,$h)
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$f", format);
        cmd.Parameters.AddWithValue("$n", filename);
        cmd.Parameters.AddWithValue("$p", fullPath);
        cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$i", count);
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.ExecuteNonQuery();
        AddAudit(cn, tx, companyId, "publish", "snapshot", null, $"{format}: {filename}");
        tx.Commit();
    }

    public static List<PublicationSnapshot> Snapshots(long companyId, int limit = 500)
    {
        var list = new List<PublicationSnapshot>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT id,company_id,format,filename,full_path,created_at,item_count,sha256
            FROM snapshots WHERE company_id=$c ORDER BY created_at DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new PublicationSnapshot
            {
                Id = r.GetInt64(0), CompanyId = r.GetInt64(1), Format = r.GetString(2),
                FileName = r.GetString(3), FullPath = r.GetString(4), CreatedAt = ParseDate(r.GetString(5)),
                ItemCount = r.GetInt32(6), Sha256 = r.GetString(7)
            });
        }
        return list;
    }

    public static List<PriceHistoryEntry> PriceHistory(long companyId, int limit = 500)
    {
        var list = new List<PriceHistoryEntry>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT id,company_id,entity_type,entity_id,entity_name,old_retail_price,new_retail_price,
                   old_anchor_price,new_anchor_price,batch_id,source,note,changed_at
            FROM price_history WHERE company_id=$c ORDER BY changed_at DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new PriceHistoryEntry
            {
                Id = r.GetInt64(0), CompanyId = r.GetInt64(1), EntityType = r.GetString(2), EntityId = r.GetInt64(3),
                EntityName = r.GetString(4), OldRetailPrice = ReadDecimal(r, 5), NewRetailPrice = ReadDecimal(r, 6),
                OldAnchorPrice = ReadDecimal(r, 7), NewAnchorPrice = ReadDecimal(r, 8), BatchId = r.GetString(9),
                Source = r.GetString(10), Note = r.GetString(11), ChangedAt = ParseDate(r.GetString(12))
            });
        }
        return list;
    }

    public static List<AuditEntry> AuditLog(long companyId, int limit = 500)
    {
        var list = new List<AuditEntry>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT id,company_id,action,entity_type,entity_id,summary,created_at
            FROM audit_log WHERE company_id=$c ORDER BY created_at DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new AuditEntry
            {
                Id = r.GetInt64(0), CompanyId = r.GetInt64(1), Action = r.GetString(2), EntityType = r.GetString(3),
                EntityId = r.IsDBNull(4) ? null : r.GetInt64(4), Summary = r.GetString(5), CreatedAt = ParseDate(r.GetString(6))
            });
        }
        return list;
    }

    public static CompanySettings GetCompanySettings(long companyId)
    {
        using var cn = Open();
        using var ensure = cn.CreateCommand();
        ensure.CommandText = """
            INSERT OR IGNORE INTO company_settings(company_id,publish_folder,only_active_publish,api_port,currency,auto_publish_enabled,auto_publish_time,in_app_notifications)
            VALUES($id,'',1,8787,'EUR',0,'11:00',1)
            """;
        ensure.Parameters.AddWithValue("$id", companyId);
        ensure.ExecuteNonQuery();

        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT publish_folder,only_active_publish,api_port,currency,auto_publish_enabled,auto_publish_time,in_app_notifications
            FROM company_settings WHERE company_id=$id
            """;
        cmd.Parameters.AddWithValue("$id", companyId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return new CompanySettings();
        return new CompanySettings
        {
            PublishFolder = r.GetString(0),
            OnlyActiveOnPublish = r.GetInt64(1) == 1,
            ApiPort = r.GetInt32(2),
            Currency = r.GetString(3),
            AutoPublishEnabled = r.GetInt64(4) == 1,
            AutoPublishTime = r.GetString(5),
            InAppNotifications = r.GetInt64(6) == 1
        };
    }

    public static void SaveCompanySettings(long companyId, CompanySettings settings)
    {
        if (settings.ApiPort is < 1024 or > 65535)
            throw new InvalidOperationException("API port mora biti između 1024 i 65535.");
        if (settings.Currency.Length != 3 || !settings.Currency.All(char.IsLetter))
            throw new InvalidOperationException("Valuta mora biti ISO oznaka od tri slova, npr. EUR.");
        if (!TimeOnly.TryParseExact(settings.AutoPublishTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidOperationException("Vrijeme automatske objave mora biti u obliku HH:mm.");

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO company_settings(company_id,publish_folder,only_active_publish,api_port,currency,auto_publish_enabled,auto_publish_time,in_app_notifications)
            VALUES($id,$folder,$active,$port,$currency,$auto,$time,$notifications)
            ON CONFLICT(company_id) DO UPDATE SET
              publish_folder=excluded.publish_folder,
              only_active_publish=excluded.only_active_publish,
              api_port=excluded.api_port,
              currency=excluded.currency,
              auto_publish_enabled=excluded.auto_publish_enabled,
              auto_publish_time=excluded.auto_publish_time,
              in_app_notifications=excluded.in_app_notifications
            """;
        cmd.Parameters.AddWithValue("$id", companyId);
        cmd.Parameters.AddWithValue("$folder", settings.PublishFolder.Trim());
        cmd.Parameters.AddWithValue("$active", settings.OnlyActiveOnPublish ? 1 : 0);
        cmd.Parameters.AddWithValue("$port", settings.ApiPort);
        cmd.Parameters.AddWithValue("$currency", settings.Currency.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("$auto", settings.AutoPublishEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$time", settings.AutoPublishTime);
        cmd.Parameters.AddWithValue("$notifications", settings.InAppNotifications ? 1 : 0);
        cmd.ExecuteNonQuery();
        AddAudit(cn, tx, companyId, "update", "settings", null, "Postavke tvrtke ažurirane");
        tx.Commit();
    }

    public static List<AppNotification> Notifications(long companyId, int limit = 50)
    {
        var list = new List<AppNotification>();
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT id,company_id,kind,title,message,is_read,created_at
            FROM notifications WHERE company_id=$c ORDER BY created_at DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new AppNotification
            {
                Id = r.GetInt64(0), CompanyId = r.GetInt64(1), Kind = r.GetString(2),
                Title = r.GetString(3), Message = r.GetString(4), IsRead = r.GetInt64(5) == 1,
                CreatedAt = ParseDate(r.GetString(6))
            });
        }
        return list;
    }

    public static int UnreadNotificationCount(long companyId)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM notifications WHERE company_id=$c AND is_read=0";
        cmd.Parameters.AddWithValue("$c", companyId);
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public static void AddNotification(long companyId, string title, string message, string kind = "info")
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO notifications(company_id,kind,title,message,is_read,created_at)
            VALUES($c,$k,$t,$m,0,$d)
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$k", string.IsNullOrWhiteSpace(kind) ? "info" : kind.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$t", title.Trim());
        cmd.Parameters.AddWithValue("$m", message.Trim());
        cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();

        using var prune = cn.CreateCommand();
        prune.CommandText = """
            DELETE FROM notifications
            WHERE company_id=$c AND id NOT IN (
                SELECT id FROM notifications WHERE company_id=$c ORDER BY created_at DESC LIMIT 500
            )
            """;
        prune.Parameters.AddWithValue("$c", companyId);
        prune.ExecuteNonQuery();
    }

    public static void MarkNotificationsRead(long companyId)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE notifications SET is_read=1 WHERE company_id=$c AND is_read=0";
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.ExecuteNonQuery();
    }

    public static void DeleteSimple(string table, long companyId, long id)
    {
        if (table is not ("categories" or "brands")) throw new InvalidOperationException("Nedopuštena tablica.");
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        var name = EntityName(cn, tx, table, companyId, id);
        if (table == "categories")
        {
            // parent_id was added by ALTER TABLE on legacy databases, so enforce ON DELETE SET NULL in application code too.
            using var detach = cn.CreateCommand();
            detach.Transaction = tx;
            detach.CommandText = "UPDATE categories SET parent_id=NULL,updated_at=$u WHERE parent_id=$id AND company_id=$c";
            detach.Parameters.AddWithValue("$id", id);
            detach.Parameters.AddWithValue("$c", companyId);
            detach.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));
            detach.ExecuteNonQuery();
        }

        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"DELETE FROM {table} WHERE id=$id AND company_id=$c";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.ExecuteNonQuery();
        AddAudit(cn, tx, companyId, "delete", table == "categories" ? "category" : "brand", id, name);
        tx.Commit();
    }

    private static void EnsureOwned(SqliteConnection cn, SqliteTransaction tx, string table, long companyId, long? id)
    {
        if (!id.HasValue) return;
        if (table is not ("categories" or "brands")) throw new InvalidOperationException("Nedopuštena tablica.");
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE id=$id AND company_id=$c";
        cmd.Parameters.AddWithValue("$id", id.Value);
        cmd.Parameters.AddWithValue("$c", companyId);
        if (Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
            throw new InvalidOperationException("Odabrani povezani podatak ne pripada odabranoj tvrtki.");
    }

    private static bool WouldCreateCategoryCycle(SqliteConnection cn, SqliteTransaction tx, long companyId, long categoryId, long parentId)
    {
        if (categoryId == 0) return false;
        var current = parentId;
        var visited = new HashSet<long>();
        while (current > 0 && visited.Add(current))
        {
            if (current == categoryId) return true;
            using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT parent_id FROM categories WHERE id=$id AND company_id=$c";
            cmd.Parameters.AddWithValue("$id", current);
            cmd.Parameters.AddWithValue("$c", companyId);
            var next = cmd.ExecuteScalar();
            if (next is null || next == DBNull.Value) return false;
            current = Convert.ToInt64(next, CultureInfo.InvariantCulture);
        }
        return false;
    }

    private static string NormalizePermissions(string permissions)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "all", "company.read", "taxonomy.read", "products.read", "services.read", "catalog.read"
        };
        var values = (permissions ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(allowed.Contains)
            .Select(x => x.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Contains("all", StringComparer.OrdinalIgnoreCase)) return "all";
        return values.Length == 0 ? "catalog.read" : string.Join(',', values);
    }

    private static bool PermissionAllows(string permissions, string required)
    {
        if (string.Equals(required, "all", StringComparison.OrdinalIgnoreCase)) return true;
        var values = permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Contains("all", StringComparer.OrdinalIgnoreCase)) return true;
        if (values.Contains(required, StringComparer.OrdinalIgnoreCase)) return true;
        if (string.Equals(required, "products.read", StringComparison.OrdinalIgnoreCase) && values.Contains("catalog.read", StringComparer.OrdinalIgnoreCase)) return true;
        if (string.Equals(required, "services.read", StringComparison.OrdinalIgnoreCase) && values.Contains("catalog.read", StringComparer.OrdinalIgnoreCase)) return true;
        if (string.Equals(required, "taxonomy.read", StringComparison.OrdinalIgnoreCase) && values.Contains("catalog.read", StringComparer.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string EntityName(SqliteConnection cn, SqliteTransaction tx, string table, long companyId, long id)
    {
        if (table is not ("products" or "services" or "categories" or "brands")) return "";
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT name FROM {table} WHERE id=$id AND company_id=$c";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$c", companyId);
        return Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "";
    }

    private static void AddPriceHistory(
        SqliteConnection cn,
        SqliteTransaction tx,
        long companyId,
        string entityType,
        long entityId,
        string entityName,
        decimal? oldRetail,
        decimal? newRetail,
        decimal? oldAnchor,
        decimal? newAnchor,
        string batchId = "",
        string source = "manual",
        string note = "")
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO price_history(company_id,entity_type,entity_id,entity_name,old_retail_price,new_retail_price,old_anchor_price,new_anchor_price,batch_id,source,note,changed_at)
            VALUES($c,$t,$id,$n,$or,$nr,$oa,$na,$batch,$source,$note,$d)
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$t", entityType);
        cmd.Parameters.AddWithValue("$id", entityId);
        cmd.Parameters.AddWithValue("$n", entityName);
        cmd.Parameters.AddWithValue("$or", DbNumber(oldRetail));
        cmd.Parameters.AddWithValue("$nr", DbNumber(newRetail));
        cmd.Parameters.AddWithValue("$oa", DbNumber(oldAnchor));
        cmd.Parameters.AddWithValue("$na", DbNumber(newAnchor));
        cmd.Parameters.AddWithValue("$batch", batchId ?? "");
        cmd.Parameters.AddWithValue("$source", string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim());
        cmd.Parameters.AddWithValue("$note", note?.Trim() ?? "");
        cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public static void WriteAudit(long companyId, string action, string entityType, long? entityId, string summary)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        AddAudit(cn, tx, companyId, action, entityType, entityId, summary);
        tx.Commit();
    }

    private static void AddAudit(
        SqliteConnection cn,
        SqliteTransaction tx,
        long companyId,
        string action,
        string entityType,
        long? entityId,
        string summary)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO audit_log(company_id,action,entity_type,entity_id,summary,created_at)
            VALUES($c,$a,$t,$id,$s,$d)
            """;
        cmd.Parameters.AddWithValue("$c", companyId);
        cmd.Parameters.AddWithValue("$a", action);
        cmd.Parameters.AddWithValue("$t", entityType);
        cmd.Parameters.AddWithValue("$id", (object?)entityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$s", summary);
        cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static object DbNumber(decimal? value)
        => value.HasValue ? (object)(double)value.Value : DBNull.Value;

    private static decimal? ReadDecimal(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var value = reader.GetValue(ordinal);
        if (value is decimal dec) return dec;
        if (value is double dbl) return Convert.ToDecimal(dbl, CultureInfo.InvariantCulture);
        if (value is long integer) return integer;
        if (value is string text && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static DateTime ParseDate(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
