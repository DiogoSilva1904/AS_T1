using Serilog.Core;
using Serilog.Events;
using System.Text.RegularExpressions;

public class LogMaskingEnricher : ILogEventEnricher
{
    private static readonly Regex EmailRegex = new(@"(?<=.{2}).(?=[^@]*?@)", RegexOptions.Compiled);
    private static readonly Regex CreditCardRegex = new(@"\b\d{4}-\d{4}-\d{4}-\d{4}\b", RegexOptions.Compiled);

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Mask sensitive information in all properties
        foreach (var property in logEvent.Properties)
        {
            if (property.Value is ScalarValue scalarValue && scalarValue.Value is string value)
            {
                // Mask the sensitive data
                string maskedValue = MaskSensitiveData(value);
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(property.Key, maskedValue));
            }
        }
    }

    private static string MaskSensitiveData(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // Mask email
        value = EmailRegex.Replace(value, "*");

        // Mask credit card number
        value = CreditCardRegex.Replace(value, "****-****-****-****");

        return value;
    }
}
