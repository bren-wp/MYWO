using System.Windows;
using System.Windows.Input;

namespace MYWO.Desktop;

public partial class ApiKeyCreatedDialog : Window
{
    public ApiKeyCreatedDialog(string key)
    {
        InitializeComponent();
        KeyBox.Text = key;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(KeyBox.Text);
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

}
