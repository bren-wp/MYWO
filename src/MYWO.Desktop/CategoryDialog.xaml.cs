using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MYWO.Desktop.Models;

namespace MYWO.Desktop;

public partial class CategoryDialog : Window
{
    private readonly Category _value;
    public Category Value => _value;

    public CategoryDialog(Category category, IEnumerable<Category> candidates)
    {
        InitializeComponent();
        _value = new Category
        {
            Id = category.Id,
            CompanyId = category.CompanyId,
            ParentId = category.ParentId,
            ParentName = category.ParentName,
            Name = category.Name,
            Description = category.Description,
            Kind = category.Kind,
            IsActive = category.IsActive,
            UpdatedAt = category.UpdatedAt
        };
        Heading.Text = category.Id == 0 ? "Nova kategorija" : "Uredi kategoriju";
        NameBox.Text = category.Name;
        DescriptionBox.Text = category.Description;
        ActiveBox.IsChecked = category.IsActive;
        var parents = new List<Category> { new() { Id = 0, CompanyId = category.CompanyId, Name = "Bez nadređene kategorije", IsActive = true } };
        parents.AddRange(candidates.Where(x => x.Id != category.Id));
        ParentBox.ItemsSource = parents;
        ParentBox.SelectedValue = category.ParentId ?? 0;
        foreach (ComboBoxItem item in KindBox.Items)
            if (item.Tag?.ToString() == category.Kind) KindBox.SelectedItem = item;
        if (KindBox.SelectedIndex < 0) KindBox.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Naziv kategorije je obavezan.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _value.Name = NameBox.Text.Trim();
        _value.Description = DescriptionBox.Text.Trim();
        _value.ParentId = ParentBox.SelectedValue is long id && id > 0 ? id : null;
        _value.Kind = (KindBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "both";
        _value.IsActive = ActiveBox.IsChecked == true;
        DialogResult = true;
    }

    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
