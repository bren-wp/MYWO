using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using MYWO.Desktop.Data;
using MYWO.Desktop.Models;

namespace MYWO.Desktop;

public partial class ProductDialog : Window
{
    private readonly Product _product;
    private static readonly CultureInfo Hr = CultureInfo.GetCultureInfo("hr-HR");
    public Product Value => _product;

    public ProductDialog(long companyId, Product product)
    {
        InitializeComponent();
        _product = product;

        var categories = new List<Category> { new() { Id = 0, CompanyId = companyId, Name = "Bez kategorije" } };
        categories.AddRange(AppDb.Categories(companyId).Where(x => x.Kind is "both" or "product"));
        CategoryBox.ItemsSource = categories;

        var brands = new List<Brand> { new() { Id = 0, CompanyId = companyId, Name = "Bez brenda" } };
        brands.AddRange(AppDb.Brands(companyId));
        BrandBox.ItemsSource = brands;

        NameBox.Text = product.Name;
        CodeBox.Text = product.Code;
        CategoryBox.SelectedValue = product.CategoryId ?? 0;
        BrandBox.SelectedValue = product.BrandId ?? 0;
        UnitBox.Text = product.Unit;
        UnitPriceBox.Text = Format(product.UnitPrice);
        RetailPriceBox.Text = Format(product.RetailPrice);
        SpecialSaleBox.IsChecked = product.SpecialSale;
        SpecialSaleNameBox.Text = product.SpecialSaleName;
        AnchorPriceBox.Text = Format(product.AnchorPrice);
        BarcodeBox.Text = product.Barcode;
        SelectComboText(AvailabilityBox, product.Availability, "Dostupno");
        ActiveBox.IsChecked = product.IsActive;
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
        if (decimal.TryParse(text, NumberStyles.Number, Hr, out var hr) ||
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out hr))
            return hr;
        throw new InvalidOperationException($"Neispravan format cijene: '{text}'.");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _product.Name = NameBox.Text.Trim();
            _product.Code = CodeBox.Text.Trim();
            _product.CategoryId = CategoryBox.SelectedValue is long categoryId && categoryId > 0 ? categoryId : null;
            _product.BrandId = BrandBox.SelectedValue is long brandId && brandId > 0 ? brandId : null;
            _product.Unit = UnitBox.Text.Trim();
            _product.UnitPrice = DecimalValue(UnitPriceBox.Text);
            _product.RetailPrice = DecimalValue(RetailPriceBox.Text, true)!.Value;
            _product.SpecialSale = SpecialSaleBox.IsChecked == true;
            _product.SpecialSaleName = SpecialSaleNameBox.Text.Trim();
            _product.AnchorPrice = DecimalValue(AnchorPriceBox.Text);
            _product.Barcode = BarcodeBox.Text.Trim();
            _product.Availability = (AvailabilityBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Dostupno";
            _product.IsActive = ActiveBox.IsChecked == true;

            if (string.IsNullOrWhiteSpace(_product.Name)) throw new InvalidOperationException("Naziv proizvoda je obavezan.");
            if (_product.SpecialSale && string.IsNullOrWhiteSpace(_product.SpecialSaleName))
                throw new InvalidOperationException("Unesite naziv posebnog oblika prodaje.");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string Format(decimal? value) => value?.ToString("0.00", Hr) ?? "";

    private static void SelectComboText(ComboBox combo, string value, string fallback)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
        if (combo.SelectedIndex < 0) combo.Text = fallback;
    }
    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

}
