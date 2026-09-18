namespace SalesSaaS.Infrastructure.Configuration;

/// <summary>
/// Loads the Mercado Pago values from the repository's local .env file when
/// running the API directly. Docker Compose already maps these same variables
/// to the standard .NET double-underscore names.
/// </summary>
public static class MercadoPagoDotEnvConfiguration
{
    private static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["MERCADOPAGO_ACCESS_TOKEN"] = "MercadoPago:AccessToken",
        ["MERCADOPAGO_NOTIFICATION_URL"] = "MercadoPago:NotificationUrl",
        ["MERCADOPAGO_SUCCESS_URL"] = "MercadoPago:SuccessUrl",
        ["MERCADOPAGO_FAILURE_URL"] = "MercadoPago:FailureUrl",
        ["MERCADOPAGO_WEBHOOK_SECRET"] = "MercadoPago:WebhookSecret",
        ["MERCADOPAGO_USE_SANDBOX"] = "MercadoPago:UseSandbox",
        ["MERCADOPAGO_ENABLE_MOCK_CHECKOUT"] = "MercadoPago:EnableMockCheckout"
    };

    public static IEnumerable<KeyValuePair<string, string?>> Read(string contentRootPath)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        LoadFile(Path.Combine(contentRootPath, ".env"), values);

        // Permit the underscore names when the API is launched directly from a
        // terminal where a .env loader exported them to the process.
        foreach (var (environmentKey, configurationKey) in Keys)
        {
            var value = Environment.GetEnvironmentVariable(environmentKey);
            if (!string.IsNullOrWhiteSpace(value)) values[configurationKey] = value;
        }

        return values;
    }

    private static void LoadFile(string filePath, IDictionary<string, string?> values)
    {
        if (!File.Exists(filePath)) return;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase)) line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            if (!Keys.TryGetValue(key, out var configurationKey)) continue;

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            values[configurationKey] = value;
        }
    }
}
