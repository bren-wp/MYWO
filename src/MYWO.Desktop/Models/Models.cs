namespace MYWO.Desktop.Models;

public sealed class Company
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Oib { get; set; } = "";
    public string Website { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class Category
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long? ParentId { get; set; }
    public string ParentName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Kind { get; set; } = "both";
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}

public sealed class Brand
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class Product
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long? CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public long? BrandId { get; set; }
    public string BrandName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Unit { get; set; } = "kom";
    public decimal? UnitPrice { get; set; }
    public decimal RetailPrice { get; set; }
    public bool SpecialSale { get; set; }
    public string SpecialSaleName { get; set; } = "";
    public decimal? AnchorPrice { get; set; }
    public string Barcode { get; set; } = "";
    public string Availability { get; set; } = "Dostupno";
    public string Description { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}

public sealed class ServiceItem
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long? CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal RetailPrice { get; set; }
    public bool SpecialSale { get; set; }
    public string SpecialSaleName { get; set; } = "";
    public decimal? AnchorPrice { get; set; }
    public string Notes { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}

public sealed class ApiKeyRecord
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Name { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Permissions { get; set; } = "all";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public long RequestCount { get; set; }
    public long ErrorCount { get; set; }
}

public sealed class ApiUsageStats
{
    public long RequestCount { get; set; }
    public long SuccessfulRequestCount { get; set; }
    public double SuccessRate => RequestCount == 0 ? 100d : SuccessfulRequestCount * 100d / RequestCount;
}

public sealed class PublicationSnapshot
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Format { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
    public string Sha256 { get; set; } = "";
    public bool FileExists => !string.IsNullOrWhiteSpace(FullPath) && File.Exists(FullPath);
}

public sealed class PriceHistoryEntry
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public string EntityName { get; set; } = "";
    public decimal? OldRetailPrice { get; set; }
    public decimal? NewRetailPrice { get; set; }
    public decimal? OldAnchorPrice { get; set; }
    public decimal? NewAnchorPrice { get; set; }
    public string BatchId { get; set; } = "";
    public string Source { get; set; } = "manual";
    public string SourceDisplay => Source switch
    {
        "bulk" => "Masovno",
        "scheduled" => "Planirano",
        "undo" => "Povrat",
        _ => "Ručno"
    };
    public string Note { get; set; } = "";
    public DateTime ChangedAt { get; set; }
}

public sealed class PriceSchedule
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public string EntityName { get; set; } = "";
    public decimal? NewRetailPrice { get; set; }
    public decimal? NewAnchorPrice { get; set; }
    public DateTime EffectiveAt { get; set; }
    public string Status { get; set; } = "pending";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
}

public sealed class AuditEntry
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public long? EntityId { get; set; }
    public string Summary { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class AppNotification
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Kind { get; set; } = "info";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class DashboardStats
{
    public int Products { get; set; }
    public int ActiveProducts { get; set; }
    public int Services { get; set; }
    public int ActiveServices { get; set; }
    public int Categories { get; set; }
    public int Brands { get; set; }
    public int ApiKeys { get; set; }
    public DateTime? LastPublishedAt { get; set; }
}

public sealed class CompanySettings
{
    public string PublishFolder { get; set; } = "";
    public bool OnlyActiveOnPublish { get; set; } = true;
    public int ApiPort { get; set; } = 8787;
    public string Currency { get; set; } = "EUR";
    public bool AutoPublishEnabled { get; set; }
    public string AutoPublishTime { get; set; } = "11:00";
    public bool InAppNotifications { get; set; } = true;
}

public sealed class CsvImportResult
{
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; } = new();
    public int TotalProcessed => Inserted + Updated + Skipped;
}
