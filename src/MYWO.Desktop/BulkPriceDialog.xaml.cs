using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MYWO.Desktop;

public partial class BulkPriceDialog : Window
{
    private readonly int _itemCount;
    private readonly string _entityLabel;

    public string Target => (TargetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "retail";
    public string Mode => (ModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "percent";
    public decimal Value { get; private set; }
    public decimal Rounding { get; private set; } = 0.01m;
    public string Note => NoteBox.Text.Trim();
    public bool ApplyNow => ApplyNowBox.IsChecked == true;
    public DateTime EffectiveAtLocal { get; private set; }

    public BulkPriceDialog(int itemCount, string entityLabel)
    {
        InitializeComponent();
        _itemCount = itemCount;
        _entityLabel = entityLabel;
        SubtitleText.Text = $"{itemCount:N0} odabranih {entityLabel}";
        EffectiveDatePicker.SelectedDate = DateTime.Today.AddDays(1);
        EffectiveTimeBox.Text = "09:00";
        UpdatePreview();
    }

    private void Input_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ValueLabel.Text = Mode switch
        {
            "percent" => "Promjena (%)",
            "fixed" => "Iznos promjene (€)",
            _ => "Nova vrijednost (€)"
        };
        UpdatePreview();
    }

    private void Timing_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ScheduleFields.IsEnabled = ScheduleBox.IsChecked == true;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (PreviewText is null) return;
        var mode = Mode switch
        {
            "percent" => "postotna promjena",
            "fixed" => "fiksna promjena",
            _ => "postavljanje točne cijene"
        };
        var target = Target switch
        {
            "retail" => "MPC",
            "anchor" => "sidrene cijene",
            _ => "MPC i sidrene cijene"
        };
        PreviewText.Text = $"{_itemCount:N0} {_entityLabel} • {target} • {mode}. " +
                           (ApplyNow ? "Promjena će biti primijenjena odmah." : "Promjena će biti spremljena kao planirana i izvršena dok je MYWO pokrenut.");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDecimal(ValueBox.Text, out var value))
        {
            MessageBox.Show("Vrijednost promjene nije valjana.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var roundingRaw = (RoundingBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "0.01";
        if (!decimal.TryParse(roundingRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var rounding) || rounding <= 0) rounding = 0.01m;
        if (Mode == "set" && value < 0)
        {
            MessageBox.Show("Nova cijena ne može biti negativna.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Value = value;
        Rounding = rounding;

        if (!ApplyNow)
        {
            if (!EffectiveDatePicker.SelectedDate.HasValue || !TimeOnly.TryParseExact(EffectiveTimeBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                MessageBox.Show("Unesite valjani datum i vrijeme u formatu HH:mm.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EffectiveAtLocal = EffectiveDatePicker.SelectedDate.Value.Date.Add(time.ToTimeSpan());
            if (EffectiveAtLocal <= DateTime.Now.AddSeconds(5))
            {
                MessageBox.Show("Planirano vrijeme mora biti u budućnosti.", "MYWO", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        DialogResult = true;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        text = (text ?? "").Trim().Replace("€", "").Trim();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("hr-HR"), out value)
               || decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private void DialogHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
