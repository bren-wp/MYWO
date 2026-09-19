using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace MYWO.Desktop;

public partial class TextEntityDialog : Window
{
    public string EntityName => NameBox.Text.Trim();
    public string Kind => (KindBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "both";
    public new bool IsActive => ActiveBox.IsChecked == true;

    public TextEntityDialog(string title, string name, string kind, bool active, bool showKind)
    {
        InitializeComponent();
        Title = title;
        Heading.Text = title;
        NameBox.Text = name;
        ActiveBox.IsChecked = active;
        KindPanel.Visibility = showKind ? Visibility.Visible : Visibility.Collapsed;
        foreach (ComboBoxItem item in KindBox.Items)
        {
            if (item.Tag?.ToString() == kind)
            {
                KindBox.SelectedItem = item;
                break;
            }
        }
        if (KindBox.SelectedIndex < 0) KindBox.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Naziv je obavezan.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

}
