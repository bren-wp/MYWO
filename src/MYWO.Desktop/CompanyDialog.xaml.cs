using System.Windows;
using System.Windows.Input;
using MYWO.Desktop.Models;
using MYWO.Desktop.Services;

namespace MYWO.Desktop;

public partial class CompanyDialog : Window
{
    private readonly Company _company;
    public Company Value => _company;

    public CompanyDialog(Company company)
    {
        InitializeComponent();
        _company = company;
        NameBox.Text = company.Name;
        OibBox.Text = company.Oib;
        WebsiteBox.Text = company.Website;
        ActiveBox.IsChecked = company.IsActive;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var oib = OibBox.Text.Trim();
        var website = WebsiteBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Naziv tvrtke je obavezan.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!ValidationService.IsValidOib(oib))
        {
            MessageBox.Show("OIB nije valjan.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!ValidationService.IsValidOptionalWebsite(website))
        {
            MessageBox.Show("Web adresa nije valjana.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _company.Name = name;
        _company.Oib = oib;
        _company.Website = website;
        _company.IsActive = ActiveBox.IsChecked == true;
        DialogResult = true;
    }
    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

}
