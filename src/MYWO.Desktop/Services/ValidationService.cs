namespace MYWO.Desktop.Services;

public static class ValidationService
{
    public static bool IsValidOib(string? oib)
    {
        if (string.IsNullOrWhiteSpace(oib)) return true;
        oib = oib.Trim();
        if (oib.Length != 11 || !oib.All(char.IsDigit)) return false;

        var a = 10;
        for (var i = 0; i < 10; i++)
        {
            a = (a + (oib[i] - '0')) % 10;
            if (a == 0) a = 10;
            a = (a * 2) % 11;
        }

        var control = 11 - a;
        if (control == 10) control = 0;
        return control == oib[10] - '0';
    }

    public static bool IsValidOptionalWebsite(string? website)
    {
        if (string.IsNullOrWhiteSpace(website)) return true;
        var value = website.Trim();
        if (!value.Contains("://", StringComparison.Ordinal)) value = "https://" + value;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && !string.IsNullOrWhiteSpace(uri.Host);
    }
}
