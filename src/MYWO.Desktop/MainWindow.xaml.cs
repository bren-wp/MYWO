using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using MYWO.Desktop.Data;
using MYWO.Desktop.Models;
using MYWO.Desktop.Services;

namespace MYWO.Desktop;

public partial class MainWindow : Window
{
    private long _companyId;
    private readonly LocalApiServer _apiServer = new();
    private string _currentPage = "dashboard";
    private bool _suppressFilters;
    private List<PublicationSnapshot> _historyCache = new();
    private List<PriceHistoryEntry> _priceHistoryCache = new();
    private Product? _editingProduct;
    private ServiceItem? _editingService;
    private readonly DispatcherTimer _scheduleTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Dictionary<long, DateOnly> _lastScheduledPublishDates = new();
    private string _publishPreviewMode = "xml";

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyWindows11Chrome();
        DatabasePathText.Text = $"Baza: {AppDb.DatabasePath}";
        var appVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.9.1";
        ReleaseModeText.Text = $"MYWO v{appVersion} • {(AppPaths.IsPortable ? "Portable" : "Instalirana verzija")}";
        SidebarVersionText.Text = $"MYWO v{appVersion}";
        _scheduleTimer.Tick += ScheduleTimer_Tick;
        _scheduleTimer.Start();
        LoadCompanies();
        Show("dashboard");
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        var width = ActualWidth > 0 ? ActualWidth : Width;
        if (width <= 0) return;

        var compact = width < 1180;
        var narrow = width < 1000;

        SidebarColumn.Width = new GridLength(compact ? (narrow ? 68 : 76) : 214);
        SidebarBrandText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SidebarPromoCard.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SidebarFooterRow.Height = new GridLength(compact ? 38 : 116);
        SidebarVersionText.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        SidebarVersionText.Margin = compact ? new Thickness(0, 6, 0, 0) : new Thickness(5, 8, 0, 0);

        foreach (var nav in new[] { NavDashboard, NavProducts, NavServices, NavCategories, NavBrands, NavPublish, NavApi, NavHistory, NavPrices, NavAudit, NavCompanies, NavSettings })
        {
            nav.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            if (nav.Content is DockPanel dock)
            {
                var labels = dock.Children.OfType<TextBlock>().ToArray();
                if (labels.Length > 1) labels[1].Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        TopbarCompanyColumn.Width = new GridLength(compact ? (narrow ? 142 : 170) : 225);
        TopbarActionsColumn.Width = new GridLength(compact ? 94 : 270);
        CompanyCombo.Width = compact ? (narrow ? 128 : 150) : 190;
        GlobalSearchContainer.Width = double.NaN;
        GlobalSearchContainer.MaxWidth = compact ? (narrow ? 330 : 420) : 520;

        TopPublishButton.Width = compact ? 42 : 145;
        TopPublishText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        TopPublishIcon.Margin = compact ? new Thickness(0) : new Thickness(0, 0, 9, 0);
        HeaderContextPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        MainPageHost.Margin = narrow ? new Thickness(12, 12, 12, 6) : compact ? new Thickness(16, 14, 16, 7) : new Thickness(20, 16, 20, 8);

        ProductDrawer.Width = narrow ? 350 : compact ? 390 : 422;
        ServiceDrawer.Width = narrow ? 350 : compact ? 390 : 420;

        SettingsCompaniesColumn.Width = new GridLength(compact ? 0.72 : 0.82, GridUnitType.Star);
        SettingsCompaniesColumn.MinWidth = narrow ? 220 : 240;
        SettingsSystemColumn.MinWidth = narrow ? 390 : 430;
        NotificationPopup.HorizontalOffset = narrow ? -260 : -310;
    }

    private void LoadCompanies()
    {
        var companies = AppDb.Companies();
        CompaniesGrid.ItemsSource = companies;

        if (_companyId == 0 || companies.All(x => x.Id != _companyId))
            _companyId = companies.FirstOrDefault()?.Id ?? 0;

        CompanyCombo.ItemsSource = companies;
        CompanyCombo.SelectedValue = _companyId;

        if (_companyId > 0)
        {
            LoadSettings();
            LoadFilterSources();
        }
    }

    private void CompanyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CompanyCombo.SelectedValue is not long id || id <= 0 || id == _companyId) return;
        _companyId = id;
        if (_apiServer.IsRunning) _apiServer.Stop();
        LoadSettings();
        LoadFilterSources();
        RefreshAll();
    }

    private void LoadSettings()
    {
        if (_companyId == 0) return;
        var settings = AppDb.GetCompanySettings(_companyId);
        var defaultFolder = System.IO.Path.Combine(AppPaths.DefaultPublishRoot, SafeFolderName(AppDb.GetCompany(_companyId).Name));

        PublishFolder.Text = string.IsNullOrWhiteSpace(settings.PublishFolder) ? defaultFolder : settings.PublishFolder;
        OnlyActivePublishCheck.IsChecked = settings.OnlyActiveOnPublish;
        SettingsPublishFolder.Text = PublishFolder.Text;
        SettingsOnlyActive.IsChecked = settings.OnlyActiveOnPublish;
        SettingsApiPort.Text = settings.ApiPort.ToString();
        SettingsCurrency.Text = settings.Currency;
        AutoPublishCheck.IsChecked = settings.AutoPublishEnabled;
        AutoPublishTimeBox.Text = settings.AutoPublishTime;
        SettingsAutoPublish.IsChecked = settings.AutoPublishEnabled;
        SettingsAutoPublishTime.Text = settings.AutoPublishTime;
        SettingsInAppNotifications.IsChecked = settings.InAppNotifications;
        UpdateNextAutoPublish(settings);
    }

    private void LoadFilterSources()
    {
        if (_companyId == 0) return;
        _suppressFilters = true;
        try
        {
            var categories = AppDb.Categories(_companyId);
            var productCategories = new List<Category>
            {
                new() { Id = 0, CompanyId = _companyId, Name = "Sve kategorije", Kind = "both", IsActive = true }
            };
            productCategories.AddRange(categories.Where(x => x.Kind is "both" or "product"));
            ProductCategoryFilter.ItemsSource = productCategories;
            ProductCategoryFilter.SelectedValue = 0L;

            var serviceCategories = new List<Category>
            {
                new() { Id = 0, CompanyId = _companyId, Name = "Sve kategorije", Kind = "both", IsActive = true }
            };
            serviceCategories.AddRange(categories.Where(x => x.Kind is "both" or "service"));
            ServiceCategoryFilter.ItemsSource = serviceCategories;
            ServiceCategoryFilter.SelectedValue = 0L;

            var brands = new List<Brand>
            {
                new() { Id = 0, CompanyId = _companyId, Name = "Svi brendovi", IsActive = true }
            };
            brands.AddRange(AppDb.Brands(_companyId));
            ProductBrandFilter.ItemsSource = brands;
            ProductBrandFilter.SelectedValue = 0L;
        }
        finally
        {
            _suppressFilters = false;
        }
    }


    private void RefreshAll()
    {
        if (_companyId == 0) return;

        var company = AppDb.GetCompany(_companyId);
        var stats = AppDb.Stats(_companyId);
        var allProducts = AppDb.Products(_companyId, "", null, "", null, null);
        var allServices = AppDb.Services(_companyId, "", null, null);

        StatProducts.Text = stats.Products.ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        StatActiveProducts.Text = $"{stats.ActiveProducts:N0} aktivnih";
        StatServices.Text = stats.Services.ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        StatActiveServices.Text = $"{stats.ActiveServices:N0} aktivnih";
        StatCategories.Text = stats.Categories.ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        StatBrands.Text = stats.Brands.ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        StatApi.Text = stats.ApiKeys.ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));

        LastPublishText.Text = stats.LastPublishedAt.HasValue
            ? stats.LastPublishedAt.Value.ToLocalTime().ToString("dd.MM.yyyy.") + Environment.NewLine + stats.LastPublishedAt.Value.ToLocalTime().ToString("HH:mm")
            : "Još nema" + Environment.NewLine + "objava";

        HeaderDateText.Text = DateTime.Now.ToString("dddd, d. MMMM yyyy.", CultureInfo.GetCultureInfo("hr-HR"));
        HeaderCompanyText.Text = $"▦  {company.Name}";
        PublishCompanyText.Text = company.Name;
        PublishLastTimeText.Text = stats.LastPublishedAt.HasValue
            ? stats.LastPublishedAt.Value.ToLocalTime().ToString("dd.MM.yyyy. 'u' HH:mm")
            : "Još nema objave";

        RefreshProducts();
        RefreshServices();

        var categories = AppDb.Categories(_companyId);
        var brands = AppDb.Brands(_companyId);
        var apiKeys = AppDb.ApiKeys(_companyId);
        var apiUsage = AppDb.ApiUsage(_companyId);
        _historyCache = AppDb.Snapshots(_companyId);
        _priceHistoryCache = AppDb.PriceHistory(_companyId);

        CategoriesGrid.ItemsSource = categories;
        BrandsGrid.ItemsSource = brands;
        BrandsPreviewGrid.ItemsSource = brands.Take(12).ToList();
        ApiGrid.ItemsSource = apiKeys;
        AuditGrid.ItemsSource = AppDb.AuditLog(_companyId);
        CompaniesGrid.ItemsSource = AppDb.Companies();
        SettingsCompaniesList.ItemsSource = AppDb.Companies();

        DashboardHistoryGrid.ItemsSource = _historyCache.Take(5).ToList();
        PublishFilesGrid.ItemsSource = _historyCache.Take(6).ToList();
        PriceHistoryGrid.ItemsSource = _priceHistoryCache;
        var schedules = AppDb.PriceSchedules(_companyId);
        ScheduledPricesGrid.ItemsSource = schedules;
        ScheduledPriceCountText.Text = schedules.Count == 0 ? "Nema planiranih" : $"{schedules.Count:N0} na čekanju";

        RefreshHistory();
        RefreshPriceAnalytics();

        ApiActiveCountText.Text = apiKeys.Count(x => x.IsActive).ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        ApiRequestCountText.Text = apiUsage.RequestCount.ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        ApiSuccessRateText.Text = apiUsage.SuccessRate.ToString("0.0", CultureInfo.GetCultureInfo("hr-HR")) + "%";
        RefreshNotifications();
        UpdateNextAutoPublish(AppDb.GetCompanySettings(_companyId));
        QuickInactiveProductsText.Text = allProducts.Count(x => !x.IsActive).ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        QuickRecentServicesText.Text = allServices.Count(x => x.UpdatedAt >= DateTime.Now.AddDays(-30)).ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        QuickPriceChangesText.Text = _priceHistoryCache.Count(x => x.ChangedAt >= DateTime.Now.AddDays(-30)).ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        QuickApiText.Text = apiKeys.Count(x => x.IsActive).ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));

        ApiServerStatus.Text = _apiServer.IsRunning
            ? $"Radi: 127.0.0.1:{_apiServer.Port}"
            : "Zaustavljen";
        ApiServerButton.Content = _apiServer.IsRunning ? "Zaustavi API" : "Pokreni API";
        PublishApiCardText.Text = _apiServer.IsRunning ? $"Port {_apiServer.Port}" : "Zaustavljen";
        CompanyScopeText.Text = $"Tvrtka: {company.Name}   •   Ctrl+N nova stavka   •   Ctrl+F pretraga   •   F5 osvježi";

        LoadPublishPreview();

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            DrawPriceChart(DashboardPriceChartCanvas, _priceHistoryCache);
            DrawPriceChart(PriceChartCanvas, _priceHistoryCache);
        }));
    }

    private void RefreshProducts()
    {
        if (_companyId == 0 || _suppressFilters) return;
        var status = (ProductStatusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
        bool? active = status switch { "active" => true, "inactive" => false, _ => null };
        var availability = (ProductAvailabilityFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        var categoryId = ProductCategoryFilter.SelectedValue is long c && c > 0 ? c : (long?)null;
        var brandId = ProductBrandFilter.SelectedValue is long b && b > 0 ? b : (long?)null;
        var items = AppDb.Products(_companyId, ProductSearch.Text ?? "", active, availability, categoryId, brandId);
        ProductsGrid.ItemsSource = items;
        ProductsCountText.Text = $"{items.Count} stavki";
    }

    private void RefreshServices()
    {
        if (_companyId == 0 || _suppressFilters) return;
        var status = (ServiceStatusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
        bool? active = status switch { "active" => true, "inactive" => false, _ => null };
        var categoryId = ServiceCategoryFilter.SelectedValue is long c && c > 0 ? c : (long?)null;
        var items = AppDb.Services(_companyId, ServiceSearch.Text ?? "", active, categoryId);
        ServicesGrid.ItemsSource = items;
        ServicesCountText.Text = $"{items.Count} stavki";
    }


    private void RefreshHistory()
    {
        if (_historyCache.Count == 0)
        {
            HistoryGrid.ItemsSource = Array.Empty<PublicationSnapshot>();
            UpdateHistoryDetails(null);
            return;
        }

        var q = HistorySearch.Text?.Trim() ?? "";
        var format = (HistoryFormatFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        IEnumerable<PublicationSnapshot> items = _historyCache;

        if (!string.IsNullOrWhiteSpace(q))
            items = items.Where(x =>
                x.FileName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Sha256.Contains(q, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(format))
            items = items.Where(x => string.Equals(x.Format, format, StringComparison.OrdinalIgnoreCase));

        var filtered = items.ToList();
        HistoryGrid.ItemsSource = filtered;

        if (HistoryGrid.SelectedItem is not PublicationSnapshot selected ||
            filtered.All(x => x.Id != selected.Id))
        {
            HistoryGrid.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        }

        UpdateHistoryDetails(HistoryGrid.SelectedItem as PublicationSnapshot);
    }

    private void UpdateHistoryDetails(PublicationSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            HistoryDetailsName.Text = "Odaberite objavu";
            HistoryDetailsDate.Text = "";
            HistoryDetailsHash.Text = "";
            HistoryDetailsPath.Text = "—";
            HistoryDetailsCount.Text = "0 stavki";
            return;
        }

        HistoryDetailsName.Text = snapshot.FileName;
        HistoryDetailsDate.Text = $"Objavljeno {snapshot.CreatedAt.ToLocalTime():dd.MM.yyyy. 'u' HH:mm}";
        HistoryDetailsHash.Text = $"SHA-256  {snapshot.Sha256}";
        HistoryDetailsPath.Text = string.IsNullOrWhiteSpace(snapshot.FullPath) ? snapshot.FileName : snapshot.FullPath;
        HistoryDetailsCount.Text = $"{snapshot.ItemCount:N0} stavki • {snapshot.Format}";
    }

    private void RefreshPriceAnalytics()
    {
        var last30 = _priceHistoryCache
            .Where(x => x.ChangedAt >= DateTime.Now.AddDays(-30))
            .ToList();

        var retailChanges = last30
            .Where(x => x.OldRetailPrice.HasValue && x.NewRetailPrice.HasValue)
            .ToList();

        var increases = retailChanges.Count(x => x.NewRetailPrice > x.OldRetailPrice);
        var decreases = retailChanges.Count(x => x.NewRetailPrice < x.OldRetailPrice);
        var affected = last30.Select(x => (x.EntityType, x.EntityId)).Distinct().Count();

        var percentages = retailChanges
            .Where(x => x.OldRetailPrice.HasValue && x.OldRetailPrice.Value != 0 && x.NewRetailPrice.HasValue)
            .Select(x => (double)((x.NewRetailPrice!.Value - x.OldRetailPrice!.Value) / x.OldRetailPrice.Value * 100m))
            .ToList();

        PriceIncreaseText.Text = increases.ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        PriceDecreaseText.Text = decreases.ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        PriceAffectedText.Text = affected.ToString("N0", CultureInfo.GetCultureInfo("hr-HR"));
        PriceAverageText.Text = percentages.Count == 0
            ? "0,0%"
            : $"{percentages.Average():+0.0;-0.0;0.0}%";
    }

    private void DrawPriceChart(Canvas canvas, IReadOnlyList<PriceHistoryEntry> source)
    {
        canvas.Children.Clear();
        var width = Math.Max(canvas.ActualWidth, 360);
        var height = Math.Max(canvas.ActualHeight, 150);
        var left = 38d;
        var right = 12d;
        var top = 18d;
        var bottom = 28d;
        var chartWidth = Math.Max(1, width - left - right);
        var chartHeight = Math.Max(1, height - top - bottom);

        var from = DateTime.Today.AddDays(-29);
        var days = Enumerable.Range(0, 30).Select(i => from.AddDays(i)).ToArray();
        var grouped = source
            .Where(x => x.ChangedAt.Date >= from)
            .GroupBy(x => x.ChangedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => (
                    Up: g.Count(x => x.OldRetailPrice.HasValue && x.NewRetailPrice > x.OldRetailPrice),
                    Down: g.Count(x => x.OldRetailPrice.HasValue && x.NewRetailPrice < x.OldRetailPrice)
                ));

        var max = Math.Max(4, grouped.Count == 0 ? 4 : grouped.Values.Max(x => Math.Max(x.Up, x.Down)));
        for (var i = 0; i <= 4; i++)
        {
            var y = top + chartHeight * i / 4d;
            var line = new Line
            {
                X1 = left,
                X2 = width - right,
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(25, 49, 73)),
                StrokeThickness = 1
            };
            canvas.Children.Add(line);

            var label = new TextBlock
            {
                Text = Math.Round(max * (4 - i) / 4d).ToString("0"),
                Foreground = (Brush)FindResource("Muted2Brush"),
                FontSize = 9
            };
            Canvas.SetLeft(label, 4);
            Canvas.SetTop(label, y - 7);
            canvas.Children.Add(label);
        }

        var slot = chartWidth / days.Length;
        var barWidth = Math.Max(2.5, slot * 0.28);
        var linePoints = new PointCollection();

        for (var i = 0; i < days.Length; i++)
        {
            grouped.TryGetValue(days[i], out var values);
            var x = left + i * slot + slot / 2d;
            var upHeight = chartHeight * values.Up / max;
            var downHeight = chartHeight * values.Down / max;

            var upBar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(1, upHeight),
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(Color.FromRgb(28, 211, 133))
            };
            Canvas.SetLeft(upBar, x - barWidth - 1);
            Canvas.SetTop(upBar, top + chartHeight - upBar.Height);
            canvas.Children.Add(upBar);

            var downBar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(1, downHeight),
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(Color.FromRgb(255, 93, 105))
            };
            Canvas.SetLeft(downBar, x + 1);
            Canvas.SetTop(downBar, top + chartHeight - downBar.Height);
            canvas.Children.Add(downBar);

            var total = values.Up + values.Down;
            var lineY = top + chartHeight - chartHeight * total / Math.Max(max * 1.35, 1);
            linePoints.Add(new Point(x, lineY));

            if (i % 5 == 0 || i == days.Length - 1)
            {
                var date = new TextBlock
                {
                    Text = days[i].ToString("d.M."),
                    Foreground = (Brush)FindResource("Muted2Brush"),
                    FontSize = 9
                };
                Canvas.SetLeft(date, Math.Max(left, x - 12));
                Canvas.SetTop(date, height - 20);
                canvas.Children.Add(date);
            }
        }

        if (linePoints.Count > 1)
        {
            var polyline = new Polyline
            {
                Points = linePoints,
                Stroke = new SolidColorBrush(Color.FromRgb(172, 216, 255)),
                StrokeThickness = 1.8
            };
            canvas.Children.Add(polyline);

            foreach (var point in linePoints.Where((_, i) => i % 3 == 0))
            {
                var dot = new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Fill = new SolidColorBrush(Color.FromRgb(11, 31, 49)),
                    Stroke = new SolidColorBrush(Color.FromRgb(191, 225, 255)),
                    StrokeThickness = 1.2
                };
                Canvas.SetLeft(dot, point.X - 2.5);
                Canvas.SetTop(dot, point.Y - 2.5);
                canvas.Children.Add(dot);
            }
        }
    }

    private void LoadPublishPreview()
    {
        try
        {
            var folder = PublishFolder.Text?.Trim() ?? "";
            if (_publishPreviewMode == "xml")
            {
                var xml = string.IsNullOrWhiteSpace(folder) ? "" : System.IO.Path.Combine(folder, "cjenik.xml");
                if (!string.IsNullOrWhiteSpace(xml) && File.Exists(xml))
                {
                    var text = File.ReadAllText(xml);
                    PublishPreviewText.Text = LimitPreview(text);
                    PublishStatusFooter.Text = $"Zadnji XML: {File.GetLastWriteTime(xml):dd.MM.yyyy HH:mm}";
                    return;
                }

                PublishPreviewText.Text = BuildXmlPreview();
                PublishStatusFooter.Text = "XML pregled aktualnih podataka";
                return;
            }

            if (_publishPreviewMode == "csv")
            {
                var csv = string.IsNullOrWhiteSpace(folder) ? "" : System.IO.Path.Combine(folder, "cjenik.csv");
                if (!string.IsNullOrWhiteSpace(csv) && File.Exists(csv))
                {
                    PublishPreviewText.Text = LimitPreview(File.ReadAllText(csv));
                    PublishStatusFooter.Text = $"Zadnji CSV: {File.GetLastWriteTime(csv):dd.MM.yyyy HH:mm}";
                    return;
                }

                PublishPreviewText.Text = BuildCsvPreview();
                PublishStatusFooter.Text = "CSV pregled aktualnih podataka";
                return;
            }

            var payload = new
            {
                company = AppDb.GetCompany(_companyId),
                products = AppDb.Products(_companyId, active: OnlyActivePublishCheck.IsChecked == true ? true : null).Take(5),
                services = AppDb.Services(_companyId, active: OnlyActivePublishCheck.IsChecked == true ? true : null).Take(5),
                generatedAt = DateTimeOffset.Now,
                preview = true
            };
            PublishPreviewText.Text = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            PublishStatusFooter.Text = "API odgovor — ogledni prikaz";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Publish preview failed.", ex);
            PublishPreviewText.Text = "Pregled nije dostupan. Podaci i dalje mogu biti objavljeni.";
            PublishStatusFooter.Text = "Pregled nije dostupan";
        }
    }

    private string BuildXmlPreview()
    {
        var company = AppDb.GetCompany(_companyId);
        var products = AppDb.Products(_companyId, active: OnlyActivePublishCheck.IsChecked == true ? true : null).Take(5).ToList();
        var services = AppDb.Services(_companyId, active: OnlyActivePublishCheck.IsChecked == true ? true : null).Take(5).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<cjenik>");
        sb.AppendLine($"  <tvrtka naziv=\"{XmlEscape(company.Name)}\" oib=\"{XmlEscape(company.Oib)}\" />");
        sb.AppendLine("  <proizvodi>");
        foreach (var p in products)
            sb.AppendLine($"    <proizvod sifra=\"{XmlEscape(p.Code)}\" naziv=\"{XmlEscape(p.Name)}\" mpc=\"{p.RetailPrice.ToString("0.00", CultureInfo.InvariantCulture)}\" dostupnost=\"{XmlEscape(p.Availability)}\" />");
        sb.AppendLine("  </proizvodi>");
        sb.AppendLine("  <usluge>");
        foreach (var service in services)
            sb.AppendLine($"    <usluga naziv=\"{XmlEscape(service.Name)}\" mpc=\"{service.RetailPrice.ToString("0.00", CultureInfo.InvariantCulture)}\" />");
        sb.AppendLine("  </usluge>");
        sb.AppendLine("</cjenik>");
        return sb.ToString();
    }

    private string BuildCsvPreview()
    {
        var products = AppDb.Products(_companyId, active: OnlyActivePublishCheck.IsChecked == true ? true : null).Take(5).ToList();
        var services = AppDb.Services(_companyId, active: OnlyActivePublishCheck.IsChecked == true ? true : null).Take(5).ToList();
        var sb = new StringBuilder("vrsta;naziv;sifra;kategorija;brend;mpc;sidrena_cijena;dostupnost;aktivno\n");
        foreach (var p in products)
            sb.AppendLine(string.Join(";", "proizvod", Csv(p.Name), Csv(p.Code), Csv(p.CategoryName), Csv(p.BrandName), CsvNumber(p.RetailPrice), CsvNumber(p.AnchorPrice), Csv(p.Availability), p.IsActive ? "1" : "0"));
        foreach (var service in services)
            sb.AppendLine(string.Join(";", "usluga", Csv(service.Name), "", Csv(service.CategoryName), "", CsvNumber(service.RetailPrice), CsvNumber(service.AnchorPrice), "", service.IsActive ? "1" : "0"));
        return sb.ToString();
    }

    private static string LimitPreview(string value)
        => value.Length <= 12000 ? value : value[..12000] + Environment.NewLine + "…";

    private static string XmlEscape(string? value)
        => System.Security.SecurityElement.Escape(value ?? "") ?? "";

    private void ShowXmlPreview_Click(object sender, RoutedEventArgs e)
    {
        _publishPreviewMode = "xml";
        LoadPublishPreview();
    }

    private void ShowCsvPreview_Click(object sender, RoutedEventArgs e)
    {
        _publishPreviewMode = "csv";
        LoadPublishPreview();
    }

    private void ShowApiPreview_Click(object sender, RoutedEventArgs e)
    {
        _publishPreviewMode = "api";
        LoadPublishPreview();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key }) Show(key);
    }


    private void Show(string key)
    {
        ProductDrawer.Visibility = Visibility.Collapsed;
        ServiceDrawer.Visibility = Visibility.Collapsed;
        _editingProduct = null;
        _editingService = null;
        _currentPage = key;
        foreach (var element in new FrameworkElement[]
                 {
                     DashboardPanel, ProductsPanel, ServicesPanel, CategoriesPanel, BrandsPanel,
                     PublishPanel, ApiPanel, HistoryPanel, PricesPanel, AuditPanel, CompaniesPanel, SettingsPanel
                 })
            element.Visibility = Visibility.Collapsed;

        (string title, string subtitle, FrameworkElement panel) = key switch
        {
            "products" => ("Proizvodi", "Upravljajte svojim asortimanom proizvoda. Dodajte, uređujte i organizirajte proizvode.", (FrameworkElement)ProductsPanel),
            "services" => ("Usluge", "Upravljajte svojim uslugama, cijenama i oblicima prodaje.", (FrameworkElement)ServicesPanel),
            "categories" => ("Kategorije i brendovi", "Upravljajte kategorijama i brendovima svojih proizvoda i usluga.", (FrameworkElement)CategoriesPanel),
            "brands" => ("Brendovi", "Organizirajte brendove unutar aktivne tvrtke.", (FrameworkElement)BrandsPanel),
            "publish" => ("Objava", "Objavite svoje cjenike u datoteke ili putem API-ja. Brzo, sigurno i uvijek ažurirano.", (FrameworkElement)PublishPanel),
            "api" => ("API ključevi", "Siguran pristup vašim podacima putem API-ja. Upravljajte ključevima i pratite korištenje.", (FrameworkElement)ApiPanel),
            "history" => ("Arhiva", "Povijest svih objava cjenika i povezanih datoteka.", (FrameworkElement)HistoryPanel),
            "prices" => ("Promjene cijena", "Analizirajte sve promjene cijena i pratite trendove kroz vrijeme.", (FrameworkElement)PricesPanel),
            "audit" => ("Administracija", "Upravljajte sustavom, tvrtkama i postavkama na jednom mjestu.", (FrameworkElement)AuditPanel),
            "companies" => ("Administracija", "Upravljajte sustavom, tvrtkama i postavkama na jednom mjestu.", (FrameworkElement)CompaniesPanel),
            "settings" => ("Administracija", "Upravljajte sustavom, tvrtkama i postavkama na jednom mjestu.", (FrameworkElement)SettingsPanel),
            _ => ("Pregled", "Dobro došli natrag! Evo kratkog pregleda stanja vaših cjenika.", (FrameworkElement)DashboardPanel)
        };

        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
        panel.Visibility = Visibility.Visible;
        UpdateNavigationState(key);
        RefreshAll();
    }

    private void UpdateNavigationState(string key)
    {
        var buttons = new Dictionary<string, Button>
        {
            ["dashboard"] = NavDashboard,
            ["products"] = NavProducts,
            ["services"] = NavServices,
            ["categories"] = NavCategories,
            ["brands"] = NavBrands,
            ["publish"] = NavPublish,
            ["api"] = NavApi,
            ["history"] = NavHistory,
            ["prices"] = NavPrices,
            ["audit"] = NavAudit,
            ["companies"] = NavCompanies,
            ["settings"] = NavSettings
        };

        foreach (var pair in buttons)
            pair.Value.Style = (Style)FindResource(pair.Key == key ? "NavButtonActive" : "NavButton");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadFilterSources();
        RefreshAll();
        Status("Podaci su osvježeni.");
    }

    private void ProductFilter_Changed(object sender, RoutedEventArgs e) => RefreshProducts();
    private void ServiceFilter_Changed(object sender, RoutedEventArgs e) => RefreshServices();

    private void NewProduct_Click(object sender, RoutedEventArgs e)
        => EditProduct(new Product { CompanyId = _companyId, IsActive = true, Availability = "Dostupno", Unit = "kom" });

    private void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is Product product) EditProduct(product);
    }

    private void ProductsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProductsGrid.SelectedItem is Product product) EditProduct(product);
    }


    private void EditProduct(Product product)
    {
        _editingProduct = new Product
        {
            Id = product.Id,
            CompanyId = product.CompanyId,
            CategoryId = product.CategoryId,
            CategoryName = product.CategoryName,
            BrandId = product.BrandId,
            BrandName = product.BrandName,
            Name = product.Name,
            Code = product.Code,
            Unit = product.Unit,
            UnitPrice = product.UnitPrice,
            RetailPrice = product.RetailPrice,
            SpecialSale = product.SpecialSale,
            SpecialSaleName = product.SpecialSaleName,
            AnchorPrice = product.AnchorPrice,
            Barcode = product.Barcode,
            Availability = product.Availability,
            Description = product.Description,
            ImagePath = product.ImagePath,
            IsActive = product.IsActive,
            UpdatedAt = product.UpdatedAt
        };

        var categories = new List<Category>
        {
            new() { Id = 0, CompanyId = _companyId, Name = "Bez kategorije", Kind = "both", IsActive = true }
        };
        categories.AddRange(AppDb.Categories(_companyId).Where(x => x.Kind is "both" or "product"));
        ProductDrawerCategoryBox.ItemsSource = categories;

        var brands = new List<Brand>
        {
            new() { Id = 0, CompanyId = _companyId, Name = "Bez brenda", IsActive = true }
        };
        brands.AddRange(AppDb.Brands(_companyId));
        ProductDrawerBrandBox.ItemsSource = brands;

        ProductDrawerTitle.Text = product.Id == 0 ? "Dodaj proizvod" : "Uredi proizvod";
        ProductDrawerNameBox.Text = product.Name;
        ProductDrawerCodeBox.Text = product.Code;
        ProductDrawerBrandBox.SelectedValue = product.BrandId ?? 0;
        ProductDrawerCategoryBox.SelectedValue = product.CategoryId ?? 0;
        ProductDrawerUnitBox.Text = string.IsNullOrWhiteSpace(product.Unit) ? "kom" : product.Unit;
        ProductDrawerUnitPriceBox.Text = FormatMoney(product.UnitPrice);
        ProductDrawerRetailPriceBox.Text = FormatMoney(product.RetailPrice);
        ProductDrawerBarcodeBox.Text = product.Barcode;
        ProductDrawerSpecialSaleBox.IsChecked = product.SpecialSale;
        ProductDrawerSpecialSaleNameBox.Text = product.SpecialSaleName;
        ProductDrawerAnchorPriceBox.Text = FormatMoney(product.AnchorPrice);
        SelectComboByContent(ProductDrawerAvailabilityBox, product.Availability, "Dostupno");
        ProductDrawerDescriptionBox.Text = product.Description;
        ProductDrawerImagePathText.Text = string.IsNullOrWhiteSpace(product.ImagePath) ? "Nije odabrana" : product.ImagePath;
        LoadProductImagePreview(product.ImagePath);
        ProductDrawerActiveBox.IsChecked = product.IsActive;

        ProductDrawer.Visibility = Visibility.Visible;
        ProductDrawerNameBox.Focus();
        ProductDrawerNameBox.SelectAll();
    }

    private void DuplicateProduct_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not Product p) return;
        var copy = new Product
        {
            CompanyId = _companyId,
            CategoryId = p.CategoryId,
            BrandId = p.BrandId,
            Name = p.Name + " – kopija",
            Code = "",
            Unit = p.Unit,
            UnitPrice = p.UnitPrice,
            RetailPrice = p.RetailPrice,
            SpecialSale = p.SpecialSale,
            SpecialSaleName = p.SpecialSaleName,
            AnchorPrice = p.AnchorPrice,
            Barcode = "",
            Availability = p.Availability,
            Description = p.Description,
            ImagePath = p.ImagePath,
            IsActive = p.IsActive
        };
        EditProduct(copy);
    }

    private void ActivateProducts_Click(object sender, RoutedEventArgs e) => SetSelectedProductsActive(true);
    private void DeactivateProducts_Click(object sender, RoutedEventArgs e) => SetSelectedProductsActive(false);

    private void SetSelectedProductsActive(bool active)
    {
        var items = ProductsGrid.SelectedItems.Cast<Product>().ToArray();
        if (items.Length == 0) return;
        AppDb.SetProductsActive(_companyId, items.Select(x => x.Id), active);
        Status(active ? "Odabrani proizvodi su aktivirani." : "Odabrani proizvodi su deaktivirani.");
        RefreshAll();
    }

    private void BulkProductPrices_Click(object sender, RoutedEventArgs e)
    {
        var items = ProductsGrid.SelectedItems.Cast<Product>().ToArray();
        if (items.Length == 0)
        {
            Status("Odaberite jedan ili više proizvoda za promjenu cijena.");
            return;
        }
        OpenBulkPriceDialog("product", items.Select(x => x.Id), items.Length, "proizvoda");
    }

    private void DeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        var items = ProductsGrid.SelectedItems.Cast<Product>().ToArray();
        if (items.Length == 0) return;
        var message = items.Length == 1
            ? $"Obrisati proizvod '{items[0].Name}'?"
            : $"Trajno obrisati {items.Length} odabranih proizvoda?";
        if (!Confirm(message)) return;
        foreach (var item in items) AppDb.DeleteProduct(_companyId, item.Id);
        Status("Odabrani proizvodi su obrisani.");
        RefreshAll();
    }

    private void NewService_Click(object sender, RoutedEventArgs e)
        => EditService(new ServiceItem { CompanyId = _companyId, IsActive = true });

    private void EditService_Click(object sender, RoutedEventArgs e)
    {
        if (ServicesGrid.SelectedItem is ServiceItem service) EditService(service);
    }

    private void ServicesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ServicesGrid.SelectedItem is ServiceItem service) EditService(service);
    }


    private void EditService(ServiceItem service)
    {
        _editingService = new ServiceItem
        {
            Id = service.Id,
            CompanyId = service.CompanyId,
            CategoryId = service.CategoryId,
            CategoryName = service.CategoryName,
            Name = service.Name,
            RetailPrice = service.RetailPrice,
            SpecialSale = service.SpecialSale,
            SpecialSaleName = service.SpecialSaleName,
            AnchorPrice = service.AnchorPrice,
            Notes = service.Notes,
            IsActive = service.IsActive,
            UpdatedAt = service.UpdatedAt
        };

        var categories = new List<Category>
        {
            new() { Id = 0, CompanyId = _companyId, Name = "Bez kategorije", Kind = "both", IsActive = true }
        };
        categories.AddRange(AppDb.Categories(_companyId).Where(x => x.Kind is "both" or "service"));
        ServiceDrawerCategoryBox.ItemsSource = categories;

        ServiceDrawerTitle.Text = service.Id == 0 ? "Dodaj uslugu" : "Uredi uslugu";
        ServiceDrawerNameBox.Text = service.Name;
        ServiceDrawerCategoryBox.SelectedValue = service.CategoryId ?? 0;
        ServiceDrawerRetailPriceBox.Text = FormatMoney(service.RetailPrice);
        ServiceDrawerAnchorPriceBox.Text = FormatMoney(service.AnchorPrice);
        ServiceDrawerSpecialSaleBox.IsChecked = service.SpecialSale;
        ServiceDrawerSpecialSaleNameBox.Text = service.SpecialSaleName;
        ServiceDrawerNotesBox.Text = service.Notes;
        ServiceDrawerActiveBox.IsChecked = service.IsActive;

        ServiceDrawer.Visibility = Visibility.Visible;
        ServiceDrawerNameBox.Focus();
        ServiceDrawerNameBox.SelectAll();
    }

    private void DuplicateService_Click(object sender, RoutedEventArgs e)
    {
        if (ServicesGrid.SelectedItem is not ServiceItem s) return;
        var copy = new ServiceItem
        {
            CompanyId = _companyId,
            CategoryId = s.CategoryId,
            Name = s.Name + " – kopija",
            RetailPrice = s.RetailPrice,
            SpecialSale = s.SpecialSale,
            SpecialSaleName = s.SpecialSaleName,
            AnchorPrice = s.AnchorPrice,
            Notes = s.Notes,
            IsActive = s.IsActive
        };
        EditService(copy);
    }

    private void ActivateServices_Click(object sender, RoutedEventArgs e) => SetSelectedServicesActive(true);
    private void DeactivateServices_Click(object sender, RoutedEventArgs e) => SetSelectedServicesActive(false);

    private void SetSelectedServicesActive(bool active)
    {
        var items = ServicesGrid.SelectedItems.Cast<ServiceItem>().ToArray();
        if (items.Length == 0) return;
        AppDb.SetServicesActive(_companyId, items.Select(x => x.Id), active);
        Status(active ? "Odabrane usluge su aktivirane." : "Odabrane usluge su deaktivirane.");
        RefreshAll();
    }

    private void BulkServicePrices_Click(object sender, RoutedEventArgs e)
    {
        var items = ServicesGrid.SelectedItems.Cast<ServiceItem>().ToArray();
        if (items.Length == 0)
        {
            Status("Odaberite jednu ili više usluga za promjenu cijena.");
            return;
        }
        OpenBulkPriceDialog("service", items.Select(x => x.Id), items.Length, "usluga");
    }

    private void DeleteService_Click(object sender, RoutedEventArgs e)
    {
        var items = ServicesGrid.SelectedItems.Cast<ServiceItem>().ToArray();
        if (items.Length == 0) return;
        var message = items.Length == 1
            ? $"Obrisati uslugu '{items[0].Name}'?"
            : $"Trajno obrisati {items.Length} odabranih usluga?";
        if (!Confirm(message)) return;
        foreach (var item in items) AppDb.DeleteService(_companyId, item.Id);
        Status("Odabrane usluge su obrisane.");
        RefreshAll();
    }


    private void OpenBulkPriceDialog(string entityType, IEnumerable<long> ids, int count, string label)
    {
        try
        {
            var idArray = ids.Distinct().ToArray();
            var dialog = new BulkPriceDialog(count, label) { Owner = this };
            if (dialog.ShowDialog() != true) return;

            if (dialog.ApplyNow)
            {
                AppDb.BulkUpdatePrices(_companyId, entityType, idArray, dialog.Target, dialog.Mode, dialog.Value, dialog.Rounding, dialog.Note);
                Notify("Cijene su ažurirane", $"Promijenjene su cijene za {count:N0} {label}.", "success");
                Status($"Masovna promjena cijena završena: {count:N0} {label}.");
            }
            else
            {
                var scheduled = AppDb.SchedulePriceChanges(_companyId, entityType, idArray, dialog.Target, dialog.Mode, dialog.Value, dialog.Rounding, dialog.EffectiveAtLocal, dialog.Note);
                Notify("Promjena cijena je planirana", $"Planirano je {scheduled:N0} promjena za {dialog.EffectiveAtLocal:dd.MM.yyyy. 'u' HH:mm}.", "info");
                Status($"Planirano promjena cijena: {scheduled:N0}.");
            }
            RefreshAll();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Bulk price operation failed.", ex);
            MessageBox.Show(ex.Message, "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UndoPriceChange_Click(object sender, RoutedEventArgs e)
    {
        if (PriceHistoryGrid.SelectedItem is not PriceHistoryEntry entry)
        {
            Status("Odaberite promjenu cijene koju želite vratiti.");
            return;
        }
        if (!Confirm($"Vratiti cijenu stavke '{entry.EntityName}' na stanje prije odabrane promjene?")) return;
        try
        {
            AppDb.UndoPriceHistory(_companyId, entry.Id);
            Notify("Promjena cijene vraćena", $"Vraćena je prethodna cijena za '{entry.EntityName}'.", "success");
            Status("Odabrana promjena cijene je vraćena.");
            RefreshAll();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Undo price change failed.", ex);
            MessageBox.Show(ex.Message, "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CancelScheduledPrice_Click(object sender, RoutedEventArgs e)
    {
        if (ScheduledPricesGrid.SelectedItem is not PriceSchedule schedule)
        {
            Status("Odaberite planiranu promjenu koju želite otkazati.");
            return;
        }
        if (!Confirm($"Otkazati planiranu promjenu cijene za '{schedule.EntityName}'?")) return;
        try
        {
            AppDb.CancelPriceSchedule(_companyId, schedule.Id);
            Status("Planirana promjena cijene je otkazana.");
            RefreshAll();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Cancel scheduled price failed.", ex);
            MessageBox.Show(ex.Message, "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshPriceModule_Click(object sender, RoutedEventArgs e)
    {
        RefreshAll();
        Status("Modul promjena cijena je osvježen.");
    }

    private void CloseProductDrawer_Click(object sender, RoutedEventArgs e)
    {
        ProductDrawer.Visibility = Visibility.Collapsed;
        _editingProduct = null;
    }

    private void SaveProductDrawer_Click(object sender, RoutedEventArgs e)
    {
        if (_editingProduct is null) return;

        try
        {
            var name = ProductDrawerNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Naziv proizvoda je obavezan.");

            _editingProduct.Name = name;
            _editingProduct.Code = ProductDrawerCodeBox.Text.Trim();
            _editingProduct.BrandId = ProductDrawerBrandBox.SelectedValue is long brandId && brandId > 0 ? brandId : null;
            _editingProduct.CategoryId = ProductDrawerCategoryBox.SelectedValue is long categoryId && categoryId > 0 ? categoryId : null;
            _editingProduct.Unit = string.IsNullOrWhiteSpace(ProductDrawerUnitBox.Text) ? "kom" : ProductDrawerUnitBox.Text.Trim();
            _editingProduct.UnitPrice = ParseOptionalMoney(ProductDrawerUnitPriceBox.Text, "Cijena po jedinici");
            _editingProduct.RetailPrice = ParseRequiredMoney(ProductDrawerRetailPriceBox.Text, "Maloprodajna cijena");
            _editingProduct.Barcode = ProductDrawerBarcodeBox.Text.Trim();
            _editingProduct.SpecialSale = ProductDrawerSpecialSaleBox.IsChecked == true;
            _editingProduct.SpecialSaleName = ProductDrawerSpecialSaleNameBox.Text.Trim();
            _editingProduct.AnchorPrice = ParseOptionalMoney(ProductDrawerAnchorPriceBox.Text, "Sidrena cijena");
            _editingProduct.Availability = SelectedComboText(ProductDrawerAvailabilityBox, "Dostupno");
            _editingProduct.Description = ProductDrawerDescriptionBox.Text.Trim();
            _editingProduct.IsActive = ProductDrawerActiveBox.IsChecked == true;

            AppDb.SaveProduct(_editingProduct);
            ProductDrawer.Visibility = Visibility.Collapsed;
            _editingProduct = null;
            LoadFilterSources();
            RefreshAll();
            Status("Proizvod je spremljen.");
        }
        catch (Exception ex) { Error(ex); }
    }

    private void CloseServiceDrawer_Click(object sender, RoutedEventArgs e)
    {
        ServiceDrawer.Visibility = Visibility.Collapsed;
        _editingService = null;
    }

    private void SaveServiceDrawer_Click(object sender, RoutedEventArgs e)
    {
        if (_editingService is null) return;

        try
        {
            var name = ServiceDrawerNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Naziv usluge je obavezan.");

            _editingService.Name = name;
            _editingService.CategoryId = ServiceDrawerCategoryBox.SelectedValue is long categoryId && categoryId > 0 ? categoryId : null;
            _editingService.RetailPrice = ParseRequiredMoney(ServiceDrawerRetailPriceBox.Text, "Maloprodajna cijena");
            _editingService.AnchorPrice = ParseOptionalMoney(ServiceDrawerAnchorPriceBox.Text, "Sidrena cijena");
            _editingService.SpecialSale = ServiceDrawerSpecialSaleBox.IsChecked == true;
            _editingService.SpecialSaleName = ServiceDrawerSpecialSaleNameBox.Text.Trim();
            _editingService.Notes = ServiceDrawerNotesBox.Text.Trim();
            _editingService.IsActive = ServiceDrawerActiveBox.IsChecked == true;

            AppDb.SaveService(_editingService);
            ServiceDrawer.Visibility = Visibility.Collapsed;
            _editingService = null;
            LoadFilterSources();
            RefreshAll();
            Status("Usluga je spremljena.");
        }
        catch (Exception ex) { Error(ex); }
    }

    private static string SelectedComboText(ComboBox combo, string fallback)
        => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;

    private static void SelectComboByContent(ComboBox combo, string value, string fallback)
    {
        ComboBoxItem? fallbackItem = null;
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), fallback, StringComparison.OrdinalIgnoreCase))
                fallbackItem = item;
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedItem = fallbackItem ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private static decimal ParseRequiredMoney(string value, string field)
    {
        var parsed = ParseOptionalMoney(value, field);
        if (!parsed.HasValue) throw new InvalidOperationException($"{field} je obavezna.");
        return parsed.Value;
    }

    private static decimal? ParseOptionalMoney(string value, string field)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.GetCultureInfo("hr-HR"), out var hr))
            return hr;

        if (decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.InvariantCulture, out var inv))
            return inv;

        throw new InvalidOperationException($"{field} nije valjan broj.");
    }

    private static string FormatMoney(decimal? value)
        => value.HasValue ? value.Value.ToString("0.00", CultureInfo.GetCultureInfo("hr-HR")) : "";

    private void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Uvezi MYWO CSV",
            Filter = "CSV datoteke (*.csv)|*.csv|Sve datoteke (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var result = CsvImportService.Import(_companyId, dialog.FileName);
            LoadFilterSources();
            RefreshAll();
            var details = result.Errors.Count == 0
                ? "Nema preskočenih redaka."
                : string.Join(Environment.NewLine, result.Errors.Take(10))
                  + (result.Errors.Count > 10 ? $"\n... i još {result.Errors.Count - 10}." : "");
            MessageBox.Show(
                $"Uvoz je završen.\n\nNovo: {result.Inserted}\nAžurirano: {result.Updated}\nPreskočeno: {result.Skipped}\n\n{details}",
                "MYWO – CSV uvoz", MessageBoxButton.OK,
                result.Errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            Status("CSV uvoz je završen.");
        }
        catch (Exception ex)
        {
            Error(ex);
        }
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
        => EditCategory(new Category { CompanyId = _companyId, IsActive = true });

    private void EditCategory_Click(object sender, RoutedEventArgs e)
    {
        if (CategoriesGrid.SelectedItem is Category category) EditCategory(category);
    }

    private void EditCategory(Category category)
    {
        var dialog = new CategoryDialog(category, AppDb.Categories(_companyId)) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        AppDb.SaveCategory(dialog.Value);
        LoadFilterSources();
        RefreshAll();
        Status("Kategorija je spremljena.");
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (CategoriesGrid.SelectedItem is not Category category || !Confirm($"Obrisati kategoriju '{category.Name}'? Stavke neće biti obrisane, nego će ostati bez kategorije.")) return;
        AppDb.DeleteSimple("categories", _companyId, category.Id);
        LoadFilterSources();
        RefreshAll();
    }

    private void AddBrand_Click(object sender, RoutedEventArgs e)
        => EditBrand(new Brand { CompanyId = _companyId, IsActive = true });

    private void EditBrand_Click(object sender, RoutedEventArgs e)
    {
        if (BrandsGrid.SelectedItem is Brand brand) EditBrand(brand);
    }

    private void EditBrand(Brand brand)
    {
        var dialog = new BrandDialog(brand) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        AppDb.SaveBrand(dialog.Value);
        LoadFilterSources();
        RefreshAll();
        Status("Brend je spremljen.");
    }

    private void DeleteBrand_Click(object sender, RoutedEventArgs e)
    {
        if (BrandsGrid.SelectedItem is not Brand brand || !Confirm($"Obrisati brend '{brand.Name}'? Proizvodi neće biti obrisani, nego će ostati bez brenda.")) return;
        AppDb.DeleteSimple("brands", _companyId, brand.Id);
        LoadFilterSources();
        RefreshAll();
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Odaberite mapu za objavu" };
        if (Directory.Exists(PublishFolder.Text)) dialog.InitialDirectory = PublishFolder.Text;
        if (dialog.ShowDialog(this) == true) PublishFolder.Text = dialog.FolderName;
    }

    private void OpenPublishFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = PublishFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            OpenWithShell(folder);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void SaveCsvTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Spremi MYWO CSV predložak",
            Filter = "CSV datoteke (*.csv)|*.csv",
            FileName = "mywo-csv-predlozak.csv"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            CsvImportService.CreateTemplate(dialog.FileName);
            Status("CSV predložak je spremljen.");
        }
        catch (Exception ex) { Error(ex); }
    }


    private void PublishXml_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SavePublishPreferences();
            var result = ExportService.PublishXml(_companyId, PublishFolder.Text.Trim(), OnlyActivePublishCheck.IsChecked == true);
            Status($"XML objavljen: {result.TotalCount} stavki.");
            Notify("XML cjenik objavljen", $"Objavljeno je {result.TotalCount} stavki u XML formatu.", "success");
            RefreshAll();
            MessageBox.Show(
                $"XML objava je završena.\n\nProizvodi: {result.ProductCount}\nUsluge: {result.ServiceCount}\n\n{result.XmlPath}",
                "MYWO", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void PublishCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SavePublishPreferences();
            var result = ExportService.PublishCsv(_companyId, PublishFolder.Text.Trim(), OnlyActivePublishCheck.IsChecked == true);
            Status($"CSV objavljen: {result.TotalCount} stavki.");
            Notify("CSV cjenik objavljen", $"Objavljeno je {result.TotalCount} stavki u CSV formatu.", "success");
            RefreshAll();
            MessageBox.Show(
                $"CSV objava je završena.\n\nProizvodi: {result.ProductCount}\nUsluge: {result.ServiceCount}\n\n{result.CsvPath}",
                "MYWO", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void Publish_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SavePublishPreferences();
            var result = ExportService.Publish(_companyId, PublishFolder.Text.Trim(), OnlyActivePublishCheck.IsChecked == true);
            Status($"Objavljeno {result.TotalCount} stavki.");
            Notify("Cjenik objavljen", $"XML i CSV su uspješno generirani za {result.TotalCount} stavki.", "success");
            RefreshAll();
            MessageBox.Show(
                $"Objava je završena.\n\nProizvodi: {result.ProductCount}\nUsluge: {result.ServiceCount}\n\n{result.XmlPath}\n{result.CsvPath}",
                "MYWO", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void SavePublishSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SavePublishPreferences();
            Notify("Postavke objave spremljene", "Postavke automatske i ručne objave su ažurirane.", "success");
            Status("Postavke objave su spremljene.");
            RefreshAll();
        }
        catch (Exception ex) { Error(ex); }
    }

    private void SavePublishPreferences()
    {
        var settings = AppDb.GetCompanySettings(_companyId);
        settings.PublishFolder = PublishFolder.Text.Trim();
        settings.OnlyActiveOnPublish = OnlyActivePublishCheck.IsChecked == true;
        settings.AutoPublishEnabled = AutoPublishCheck.IsChecked == true;
        settings.AutoPublishTime = AutoPublishTimeBox.Text.Trim();
        AppDb.SaveCompanySettings(_companyId, settings);
        SettingsPublishFolder.Text = settings.PublishFolder;
        SettingsOnlyActive.IsChecked = settings.OnlyActiveOnPublish;
        SettingsAutoPublish.IsChecked = settings.AutoPublishEnabled;
        SettingsAutoPublishTime.Text = settings.AutoPublishTime;
        UpdateNextAutoPublish(settings);
    }


    private void NewApiKey_Click(object sender, RoutedEventArgs e)
    {
        var setup = new ApiKeyDialog { Owner = this };
        if (setup.ShowDialog() != true) return;

        try
        {
            var key = ApiKeyService.Create(_companyId, setup.KeyName, setup.Permissions);
            Notify("Novi API ključ", $"Generiran je ključ '{setup.KeyName}' s dozvolama: {setup.Permissions}.", "success");
            RefreshAll();
            Clipboard.SetText(key);
            var dialog = new ApiKeyCreatedDialog(key) { Owner = this };
            dialog.ShowDialog();
            Status("Novi API ključ je generiran i kopiran u međuspremnik.");
        }
        catch (Exception ex) { Error(ex); }
    }

    private void RevokeApiKey_Click(object sender, RoutedEventArgs e)
    {
        if (ApiGrid.SelectedItem is not ApiKeyRecord key || !key.IsActive) return;
        if (!Confirm($"Opozvati API ključ '{key.Name}'? Ova radnja odmah prekida njegov pristup.")) return;
        AppDb.RevokeApiKey(_companyId, key.Id);
        Notify("API ključ opozvan", $"Ključ '{key.Name}' više nema pristup API-ju.", "warning");
        RefreshAll();
    }

    private void ApiServerButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_apiServer.IsRunning)
            {
                _apiServer.Stop();
                Status("Lokalni API je zaustavljen.");
                Notify("Lokalni API zaustavljen", "API više ne prihvaća lokalne zahtjeve.", "info");
            }
            else
            {
                var settings = AppDb.GetCompanySettings(_companyId);
                _apiServer.Start(_companyId, settings.ApiPort);
                Status("Lokalni API je pokrenut.");
                Notify("Lokalni API pokrenut", $"API sluša na 127.0.0.1:{settings.ApiPort}.", "success");
            }
            RefreshAll();
        }
        catch (Exception ex) { Error(ex); }
    }

    private void VerifySnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not PublicationSnapshot snapshot) return;
        try
        {
            var ok = ExportService.VerifySnapshot(snapshot);
            Notify(ok ? "Integritet potvrđen" : "Problem s integritetom",
                ok ? $"Datoteka {snapshot.FileName} prošla je SHA-256 provjeru." : $"Datoteka {snapshot.FileName} nije prošla provjeru.",
                ok ? "success" : "error");
            MessageBox.Show(
                ok ? "SHA-256 se podudara. Datoteka nije promijenjena od objave."
                   : "Provjera nije uspjela. Datoteka ne postoji ili se SHA-256 ne podudara.",
                "MYWO – provjera objave", MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void OpenSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not PublicationSnapshot snapshot) return;
        if (!File.Exists(snapshot.FullPath))
        {
            MessageBox.Show("Datoteka više ne postoji na zabilježenoj lokaciji.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        OpenWithShell(snapshot.FullPath);
    }

    private void OpenSnapshotFolder_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not PublicationSnapshot snapshot) return;
        var folder = System.IO.Path.GetDirectoryName(snapshot.FullPath);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show("Mapa više ne postoji na zabilježenoj lokaciji.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        OpenWithShell(folder);
    }

    private void RestoreSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not PublicationSnapshot snapshot) return;
        if (!Confirm($"Vratiti verziju '{snapshot.FileName}' kao aktivnu javnu datoteku? Postojeća stabilna datoteka istog formata bit će zamijenjena.")) return;

        try
        {
            var folder = PublishFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder))
                throw new InvalidOperationException("Najprije odaberite mapu za objavu.");

            var restoredPath = ExportService.RestoreSnapshot(snapshot, folder);
            AppDb.WriteAudit(_companyId, "restore_publication", "publication_snapshot", snapshot.Id,
                $"Vraćena verzija {snapshot.FileName} kao {System.IO.Path.GetFileName(restoredPath)}.");
            Notify("Verzija cjenika vraćena", $"{snapshot.FileName} je vraćen kao aktivna {snapshot.Format.ToUpperInvariant()} datoteka.", "success");
            Status($"Vraćena objava: {snapshot.FileName}");
            RefreshAll();
            MessageBox.Show($"Verzija je uspješno vraćena.\n\nAktivna datoteka:\n{restoredPath}",
                "MYWO – vraćanje verzije", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void ChooseProductImage_Click(object sender, RoutedEventArgs e)
    {
        if (_editingProduct is null) return;
        var dialog = new OpenFileDialog
        {
            Title = "Odaberite sliku proizvoda",
            Filter = "Slike (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Sve datoteke (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var extension = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
            if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
                throw new InvalidOperationException("Podržani formati slike su PNG, JPG, JPEG i WEBP.");
            var info = new FileInfo(dialog.FileName);
            if (info.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("Slika proizvoda ne smije biti veća od 10 MB.");

            var root = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(AppDb.DatabasePath) ?? AppContext.BaseDirectory,
                "media", $"company-{_companyId}", "products");
            Directory.CreateDirectory(root);
            var target = System.IO.Path.Combine(root, $"{Guid.NewGuid():N}{extension}");
            File.Copy(dialog.FileName, target, true);
            _editingProduct.ImagePath = target;
            ProductDrawerImagePathText.Text = target;
            LoadProductImagePreview(target);
            Status("Slika proizvoda je dodana.");
        }
        catch (Exception ex) { Error(ex); }
    }

    private void RemoveProductImage_Click(object sender, RoutedEventArgs e)
    {
        if (_editingProduct is null) return;
        _editingProduct.ImagePath = "";
        ProductDrawerImagePathText.Text = "Nije odabrana";
        ProductDrawerImagePreview.Source = null;
        Status("Slika proizvoda je uklonjena iz stavke.");
    }

    private void LoadProductImagePreview(string? path)
    {
        ProductDrawerImagePreview.Source = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 800;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            ProductDrawerImagePreview.Source = bitmap;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Product image preview failed.", ex);
        }
    }

    private void NotificationButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshNotifications();
        NotificationPopup.IsOpen = !NotificationPopup.IsOpen;
    }

    private void MarkNotificationsRead_Click(object sender, RoutedEventArgs e)
    {
        if (_companyId == 0) return;
        AppDb.MarkNotificationsRead(_companyId);
        RefreshNotifications();
        Status("Obavijesti su označene kao pročitane.");
    }

    private void NotifyForCompany(long companyId, string title, string message, string kind = "info")
    {
        if (companyId <= 0) return;
        try
        {
            var settings = AppDb.GetCompanySettings(companyId);
            if (!settings.InAppNotifications) return;
            AppDb.AddNotification(companyId, title, message, kind);
            if (companyId == _companyId) RefreshNotifications();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Unable to create in-app notification.", ex);
        }
    }

    private void Notify(string title, string message, string kind = "info")
    {
        if (_companyId == 0) return;
        NotifyForCompany(_companyId, title, message, kind);
    }

    private void RefreshNotifications()
    {
        if (_companyId == 0)
        {
            NotificationList.ItemsSource = Array.Empty<AppNotification>();
            NotificationBadge.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            var notifications = AppDb.Notifications(_companyId, 12);
            NotificationList.ItemsSource = notifications;
            var unread = AppDb.UnreadNotificationCount(_companyId);
            NotificationCountText.Text = unread > 99 ? "99+" : unread.ToString(CultureInfo.InvariantCulture);
            NotificationBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
            NotificationSummaryText.Text = notifications.Count == 0
                ? "Nema novih obavijesti."
                : unread > 0 ? $"{unread} nepročitanih • {notifications.Count} prikazano" : $"{notifications.Count} nedavnih obavijesti";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Unable to refresh in-app notifications.", ex);
        }
    }

    private void UpdateNextAutoPublish(CompanySettings settings)
    {
        if (!settings.AutoPublishEnabled)
        {
            NextAutoPublishText.Text = "Automatska objava isključena";
            return;
        }

        if (!TimeOnly.TryParseExact(settings.AutoPublishTime, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
        {
            NextAutoPublishText.Text = "Neispravno vrijeme objave";
            return;
        }

        var now = DateTime.Now;
        var next = now.Date.Add(time.ToTimeSpan());
        if (next <= now) next = next.AddDays(1);
        NextAutoPublishText.Text = $"Sljedeća automatska objava: {next:dd.MM.yyyy. 'u' HH:mm}";
    }

    private void ScheduleTimer_Tick(object? sender, EventArgs e)
    {
        if (_companyId == 0) return;

        var refreshCurrent = false;
        foreach (var company in AppDb.Companies().Where(x => x.IsActive))
        {
            try
            {
                var appliedPrices = AppDb.ApplyDuePriceSchedules(company.Id);
                if (appliedPrices > 0)
                {
                    NotifyForCompany(company.Id, "Planirane cijene primijenjene", $"Primijenjeno je {appliedPrices:N0} planiranih promjena cijena.", "success");
                    if (company.Id == _companyId)
                    {
                        Status($"Primijenjeno planiranih promjena cijena: {appliedPrices:N0}.");
                        refreshCurrent = true;
                    }
                }

                var settings = AppDb.GetCompanySettings(company.Id);
                if (company.Id == _companyId) UpdateNextAutoPublish(settings);
                if (!settings.AutoPublishEnabled) continue;
                if (!TimeOnly.TryParseExact(settings.AutoPublishTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledTime)) continue;

                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);
                if (now.TimeOfDay < scheduledTime.ToTimeSpan()) continue;
                if (_lastScheduledPublishDates.TryGetValue(company.Id, out var lastDate) && lastDate == today) continue;

                var recent = AppDb.Snapshots(company.Id, 12);
                var graceStart = scheduledTime.ToTimeSpan() - TimeSpan.FromMinutes(2);
                var todayAfterSchedule = recent
                    .Where(x => x.CreatedAt.ToLocalTime().Date == now.Date && x.CreatedAt.ToLocalTime().TimeOfDay >= graceStart)
                    .Select(x => x.Format.ToUpperInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (todayAfterSchedule.Contains("XML") && todayAfterSchedule.Contains("CSV"))
                {
                    _lastScheduledPublishDates[company.Id] = today;
                    continue;
                }

                _lastScheduledPublishDates[company.Id] = today;
                var folder = settings.PublishFolder;
                if (string.IsNullOrWhiteSpace(folder))
                {
                    folder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "MYWO", "Objave", SafeFolderName(company.Name));
                }

                var result = ExportService.Publish(company.Id, folder, settings.OnlyActiveOnPublish);
                AppDb.WriteAudit(company.Id, "scheduled_publish", "publication", null,
                    $"Automatska XML/CSV objava: {result.TotalCount} stavki.");
                NotifyForCompany(company.Id, "Automatska objava završena", $"Objavljeno je {result.TotalCount} stavki u XML i CSV formatu.", "success");
                if (company.Id == _companyId)
                {
                    Status($"Automatska objava završena: {result.TotalCount} stavki.");
                    refreshCurrent = true;
                }
            }
            catch (Exception ex)
            {
                _lastScheduledPublishDates[company.Id] = DateOnly.FromDateTime(DateTime.Now);
                AppLogger.Error($"Scheduled automation failed for company {company.Id}.", ex);
                NotifyForCompany(company.Id, "Automatizacija nije uspjela", ex.Message, "error");
                if (company.Id == _companyId) Status("Automatizacija nije uspjela. Detalji su zapisani u log.");
            }
        }

        if (refreshCurrent) RefreshAll();
    }

    private void AddCompany_Click(object sender, RoutedEventArgs e) => EditCompany(new Company { IsActive = true });

    private void EditCompany_Click(object sender, RoutedEventArgs e)
    {
        if (CompaniesGrid.SelectedItem is Company company) EditCompany(company);
    }

    private void CompaniesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CompaniesGrid.SelectedItem is Company company) EditCompany(company);
    }

    private void EditCompany(Company company)
    {
        var dialog = new CompanyDialog(company) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var id = AppDb.SaveCompany(dialog.Value);
        _companyId = id;
        LoadCompanies();
        RefreshAll();
        Status("Tvrtka je spremljena.");
    }

    private void ChooseSettingsFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Odaberite zadanu mapu za objavu" };
        if (Directory.Exists(SettingsPublishFolder.Text)) dialog.InitialDirectory = SettingsPublishFolder.Text;
        if (dialog.ShowDialog(this) == true) SettingsPublishFolder.Text = dialog.FolderName;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(SettingsApiPort.Text.Trim(), out var port))
                throw new InvalidOperationException("API port nije valjan broj.");
            var settings = new CompanySettings
            {
                PublishFolder = SettingsPublishFolder.Text.Trim(),
                OnlyActiveOnPublish = SettingsOnlyActive.IsChecked == true,
                ApiPort = port,
                Currency = SettingsCurrency.Text.Trim(),
                AutoPublishEnabled = SettingsAutoPublish.IsChecked == true,
                AutoPublishTime = SettingsAutoPublishTime.Text.Trim(),
                InAppNotifications = SettingsInAppNotifications.IsChecked == true
            };
            AppDb.SaveCompanySettings(_companyId, settings);
            PublishFolder.Text = settings.PublishFolder;
            OnlyActivePublishCheck.IsChecked = settings.OnlyActiveOnPublish;
            AutoPublishCheck.IsChecked = settings.AutoPublishEnabled;
            AutoPublishTimeBox.Text = settings.AutoPublishTime;
            UpdateNextAutoPublish(settings);
            if (_apiServer.IsRunning)
            {
                _apiServer.Stop();
                Status("Postavke su spremljene. API je zaustavljen zbog promjene postavki.");
            }
            else Status("Postavke su spremljene.");
            RefreshAll();
        }
        catch (Exception ex) { Error(ex); }
    }


    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var updater = System.IO.Path.Combine(AppContext.BaseDirectory, "MYWO-Update.exe");
            if (!File.Exists(updater))
            {
                MessageBox.Show(
                    AppPaths.IsPortable
                        ? "Portable izdanje nema ugrađeni updater uz aplikaciju. Preuzmite novu MYWO-Portable.exe verziju ili pokrenite MYWO-Update.exe koji dolazi uz release paket."
                        : "MYWO-Update.exe nije pronađen u instalacijskoj mapi.",
                    "MYWO Update", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = updater, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Error(ex);
        }
    }

    private void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Izradi MYWO sigurnosnu kopiju",
            Filter = "MYWO backup (*.db)|*.db|Sve datoteke (*.*)|*.*",
            FileName = $"MYWO-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var path = BackupService.CreateBackup(dialog.FileName);
            Status("Sigurnosna kopija je izrađena.");
            Notify("Sigurnosna kopija izrađena", $"Backup je spremljen kao {System.IO.Path.GetFileName(path)}.", "success");
            MessageBox.Show($"Sigurnosna kopija je spremljena:\n\n{path}", "MYWO", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Vrati MYWO sigurnosnu kopiju",
            Filter = "SQLite / MYWO backup (*.db;*.bak)|*.db;*.bak|Sve datoteke (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;
        if (!Confirm("Vraćanje backupa zamijenit će trenutačnu bazu. Prije zamjene automatski će se izraditi sigurnosna kopija trenutne baze. Nastaviti?")) return;

        try
        {
            if (_apiServer.IsRunning) _apiServer.Stop();
            var emergency = BackupService.RestoreBackup(dialog.FileName);
            _companyId = 0;
            LoadCompanies();
            LoadFilterSources();
            RefreshAll();
            Status("Backup je vraćen.");
            Notify("Sigurnosna kopija vraćena", "Baza podataka uspješno je obnovljena iz odabrane sigurnosne kopije.", "success");
            MessageBox.Show(
                $"Backup je uspješno vraćen.\n\nPrethodna baza automatski je spremljena kao:\n{emergency}",
                "MYWO", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void IntegrityCheck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = AppDb.IntegrityCheck();
            Notify(result.Equals("ok", StringComparison.OrdinalIgnoreCase) ? "Baza je ispravna" : "Provjera baze upozorava",
                result.Equals("ok", StringComparison.OrdinalIgnoreCase) ? "SQLite integrity_check završen je bez greške." : result,
                result.Equals("ok", StringComparison.OrdinalIgnoreCase) ? "success" : "warning");
            MessageBox.Show(
                result.Equals("ok", StringComparison.OrdinalIgnoreCase)
                    ? "SQLite provjera integriteta baze: OK."
                    : "SQLite provjera je vratila: " + result,
                "MYWO – provjera baze", MessageBoxButton.OK,
                result.Equals("ok", StringComparison.OrdinalIgnoreCase) ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) { Error(ex); }
    }



    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void ApplyWindows11Chrome()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10)) return;

        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Win11: dark non-client rendering + rounded outer corners. Calls are deliberately
            // best-effort so MYWO remains compatible with older supported Windows builds.
            var darkMode = 1;
            _ = DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                var rounded = 2;
                _ = DwmSetWindowAttribute(hwnd, 33, ref rounded, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE / ROUND
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        catch (Exception ex)
        {
            AppLogger.Info($"Windows chrome styling skipped: {ex.Message}");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void GlobalSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFilters) return;
        var text = GlobalSearchBox.Text ?? "";

        if (_currentPage == "products" && ProductSearch.Text != text)
            ProductSearch.Text = text;
        else if (_currentPage == "services" && ServiceSearch.Text != text)
            ServiceSearch.Text = text;
        else if (_currentPage == "history" && HistorySearch.Text != text)
            HistorySearch.Text = text;
    }

    private void PriceChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Canvas canvas && _priceHistoryCache.Count >= 0)
            DrawPriceChart(canvas, _priceHistoryCache);
    }

    private void ProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductsGrid.SelectedItems.Count == 0) return;
        Status($"Odabrano proizvoda: {ProductsGrid.SelectedItems.Count}");
    }

    private void HistoryFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (_companyId == 0) return;
        RefreshHistory();
    }

    private void ResetHistoryFilters_Click(object sender, RoutedEventArgs e)
    {
        HistorySearch.Text = "";
        HistoryFormatFilter.SelectedIndex = 0;
        RefreshHistory();
    }

    private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateHistoryDetails(HistoryGrid.SelectedItem as PublicationSnapshot);

    private void VerifyLatestPublish_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _historyCache.FirstOrDefault();
        if (snapshot is null)
        {
            MessageBox.Show("Još nema objavljenih datoteka za provjeru.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var ok = ExportService.VerifySnapshot(snapshot);
            Status(ok ? "Integritet zadnje objave je potvrđen." : "Integritet zadnje objave nije potvrđen.");
            MessageBox.Show(
                ok ? "SHA-256 zadnje objave se podudara." : "Datoteka nedostaje ili se SHA-256 ne podudara.",
                "MYWO – integritet", MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void CopyApiEndpoint_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppDb.GetCompanySettings(_companyId);
        var endpoint = $"http://127.0.0.1:{settings.ApiPort}/api/v1/catalog";
        Clipboard.SetText(endpoint);
        Status("API endpoint je kopiran u međuspremnik.");
    }

    private void ExportProductsCsv_Click(object sender, RoutedEventArgs e)
    {
        var items = ProductsGrid.ItemsSource?.Cast<Product>().ToList() ?? new List<Product>();
        var dialog = new SaveFileDialog
        {
            Title = "Izvezi proizvode u CSV",
            Filter = "CSV datoteke (*.csv)|*.csv",
            FileName = $"mywo-proizvodi-{DateTime.Now:yyyyMMdd-HHmm}.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Naziv;Šifra;Marka;Kategorija;JM;Cijena/JM;MPC;Posebna prodaja;Naziv posebne prodaje;Sidrena cijena;Barkod;Dostupnost;Aktivno;Opis");
            foreach (var x in items)
            {
                sb.AppendLine(string.Join(";",
                    Csv(x.Name), Csv(x.Code), Csv(x.BrandName), Csv(x.CategoryName), Csv(x.Unit),
                    CsvNumber(x.UnitPrice), CsvNumber(x.RetailPrice), x.SpecialSale ? "Da" : "Ne",
                    Csv(x.SpecialSaleName), CsvNumber(x.AnchorPrice), Csv(x.Barcode), Csv(x.Availability),
                    x.IsActive ? "Da" : "Ne", Csv(x.Description)));
            }
            File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
            Status($"Izvezeno {items.Count} proizvoda.");
        }
        catch (Exception ex) { Error(ex); }
    }

    private void ExportServicesCsv_Click(object sender, RoutedEventArgs e)
    {
        var items = ServicesGrid.ItemsSource?.Cast<ServiceItem>().ToList() ?? new List<ServiceItem>();
        var dialog = new SaveFileDialog
        {
            Title = "Izvezi usluge u CSV",
            Filter = "CSV datoteke (*.csv)|*.csv",
            FileName = $"mywo-usluge-{DateTime.Now:yyyyMMdd-HHmm}.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Naziv;Kategorija;MPC;Posebna prodaja;Naziv posebne prodaje;Sidrena cijena;Aktivno;Zadnja izmjena;Napomena");
            foreach (var x in items)
            {
                sb.AppendLine(string.Join(";",
                    Csv(x.Name), Csv(x.CategoryName), CsvNumber(x.RetailPrice),
                    x.SpecialSale ? "Da" : "Ne", Csv(x.SpecialSaleName), CsvNumber(x.AnchorPrice),
                    x.IsActive ? "Da" : "Ne", Csv(x.UpdatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")), Csv(x.Notes)));
            }
            File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
            Status($"Izvezeno {items.Count} usluga.");
        }
        catch (Exception ex) { Error(ex); }
    }

    private static string Csv(string? value)
    {
        value ??= "";
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string CsvNumber(decimal? value)
        => value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) : "";

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            Refresh_Click(sender, e);
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

        if (e.Key == Key.K)
        {
            GlobalSearchBox.Focus();
            GlobalSearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F)
        {
            if (_currentPage == "products") ProductSearch.Focus();
            else if (_currentPage == "services") ServiceSearch.Focus();
            else if (_currentPage == "history") HistorySearch.Focus();
            else GlobalSearchBox.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.N) return;
        switch (_currentPage)
        {
            case "products": NewProduct_Click(sender, e); break;
            case "services": NewService_Click(sender, e); break;
            case "categories": AddCategory_Click(sender, e); break;
            case "brands": AddBrand_Click(sender, e); break;
            case "companies": AddCompany_Click(sender, e); break;
            default: return;
        }
        e.Handled = true;
    }

    private static bool Confirm(string message)
        => MessageBox.Show(message, "MYWO", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void Status(string message) => StatusText.Text = message;

    private static void Error(Exception ex)
    {
        AppLogger.Error("Operation failed.", ex);
        MessageBox.Show(ex.Message, "MYWO", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private string? Prompt(string title, string value)
    {
        var dialog = new PromptDialog(title, value) { Owner = this };
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    private static void OpenWithShell(string path)
        => Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });

    private static string SafeFolderName(string value)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim();
    }

    protected override void OnClosed(EventArgs e)
    {
        _scheduleTimer.Stop();
        _apiServer.Dispose();
        base.OnClosed(e);
    }
}
