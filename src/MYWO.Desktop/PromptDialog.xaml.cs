using System.Windows;
using System.Windows.Input;

namespace MYWO.Desktop;

public partial class PromptDialog : Window
{
    public string Value => ValueBox.Text.Trim();

    public PromptDialog(string title, string value)
    {
        InitializeComponent();
        Title = title;
        Heading.Text = title;
        ValueBox.Text = value;
        ValueBox.SelectAll();
        ValueBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ValueBox.Text)) return;
        DialogResult = true;
    }
    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

}
