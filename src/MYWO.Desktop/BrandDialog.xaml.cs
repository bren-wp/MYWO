using System.Windows;
using System.Windows.Input;
using MYWO.Desktop.Models;

namespace MYWO.Desktop;

public partial class BrandDialog : Window
{
    private readonly Brand _value;
    public Brand Value => _value;

    public BrandDialog(Brand brand)
    {
        InitializeComponent();
        _value = new Brand { Id = brand.Id, CompanyId = brand.CompanyId, Name = brand.Name, Description = brand.Description, IsActive = brand.IsActive };
        Heading.Text = brand.Id == 0 ? "Novi brend" : "Uredi brend";
        NameBox.Text = brand.Name;
        DescriptionBox.Text = brand.Description;
        ActiveBox.IsChecked = brand.IsActive;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Naziv brenda je obavezan.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _value.Name = NameBox.Text.Trim();
        _value.Description = DescriptionBox.Text.Trim();
        _value.IsActive = ActiveBox.IsChecked == true;
        DialogResult = true;
    }

    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
