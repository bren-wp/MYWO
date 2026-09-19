using System.Globalization;
using System.Windows;
using System.Windows.Input;
using MYWO.Desktop.Data;
using MYWO.Desktop.Models;

namespace MYWO.Desktop;

public partial class ServiceDialog : Window
{
    private readonly ServiceItem _service;
    private static readonly CultureInfo Hr = CultureInfo.GetCultureInfo("hr-HR");
    public ServiceItem Value => _service;

    public ServiceDialog(long companyId, ServiceItem service)
    {
        InitializeComponent();
        _service = service;
        var categories = new List<Category> { new() { Id = 0, CompanyId = companyId, Name = "Bez kategorije" } };
        categories.AddRange(AppDb.Categories(companyId).Where(x => x.Kind is "both" or "service"));
        CategoryBox.ItemsSource = categories;
        NameBox.Text = service.Name;
        CategoryBox.SelectedValue = service.CategoryId ?? 0;
        RetailPriceBox.Text = Format(service.RetailPrice);
        SpecialSaleBox.IsChecked = service.SpecialSale;
        SpecialSaleNameBox.Text = service.SpecialSaleName;
        AnchorPriceBox.Text = Format(service.AnchorPrice);
        ActiveBox.IsChecked = service.IsActive;
        UpdateSpecialSaleState();
    }

    private void SpecialSaleBox_Changed(object sender, RoutedEventArgs e) => UpdateSpecialSaleState();

    private void UpdateSpecialSaleState()
    {
        var enabled = SpecialSaleBox.IsChecked == true;
        SpecialSaleNameBox.IsEnabled = enabled;
        AnchorPriceBox.IsEnabled = enabled || !string.IsNullOrWhiteSpace(AnchorPriceBox.Text);
        if (!enabled) SpecialSaleNameBox.Text = "";
    }

    private static decimal? DecimalValue(string text, bool required = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (required) throw new InvalidOperationException("Maloprodajna cijena je obavezna.");
            return null;
        }
        if (decimal.TryParse(text, NumberStyles.Number, Hr, out var parsed) ||
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            return parsed;
        throw new InvalidOperationException($"Neispravan format cijene: '{text}'.");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _service.Name = NameBox.Text.Trim();
            _service.CategoryId = CategoryBox.SelectedValue is long categoryId && categoryId > 0 ? categoryId : null;
            _service.RetailPrice = DecimalValue(RetailPriceBox.Text, true)!.Value;
            _service.SpecialSale = SpecialSaleBox.IsChecked == true;
            _service.SpecialSaleName = SpecialSaleNameBox.Text.Trim();
            _service.AnchorPrice = DecimalValue(AnchorPriceBox.Text);
            _service.IsActive = ActiveBox.IsChecked == true;
            if (string.IsNullOrWhiteSpace(_service.Name)) throw new InvalidOperationException("Naziv usluge je obavezan.");
            if (_service.SpecialSale && string.IsNullOrWhiteSpace(_service.SpecialSaleName))
                throw new InvalidOperationException("Unesite naziv posebnog oblika prodaje.");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string Format(decimal? value) => value?.ToString("0.00", Hr) ?? "";
    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

}
