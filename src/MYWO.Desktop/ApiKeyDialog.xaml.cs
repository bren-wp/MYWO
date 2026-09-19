using System.Windows;
using System.Windows.Input;

namespace MYWO.Desktop;

public partial class ApiKeyDialog : Window
{
    public string KeyName => NameBox.Text.Trim();
    public string Permissions
    {
        get
        {
            if (AllBox.IsChecked == true) return "all";
            var values = new List<string>();
            if (CompanyBox.IsChecked == true) values.Add("company.read");
            if (TaxonomyBox.IsChecked == true) values.Add("taxonomy.read");
            if (ProductsBox.IsChecked == true) values.Add("products.read");
            if (ServicesBox.IsChecked == true) values.Add("services.read");
            if (CatalogBox.IsChecked == true) values.Add("catalog.read");
            return values.Count == 0 ? "catalog.read" : string.Join(',', values);
        }
    }

    public ApiKeyDialog()
    {
        InitializeComponent();
        NameBox.Text = "Web integracija";
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void AllBox_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = AllBox.IsChecked != true;
        CompanyBox.IsEnabled = enabled;
        TaxonomyBox.IsEnabled = enabled;
        ProductsBox.IsEnabled = enabled;
        ServicesBox.IsEnabled = enabled;
        CatalogBox.IsEnabled = enabled;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Naziv API ključa je obavezan.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
