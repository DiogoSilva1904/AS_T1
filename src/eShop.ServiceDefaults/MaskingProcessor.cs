using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.RegularExpressions;

public class MaskingProcessor : BaseProcessor<Activity>
{
    private static string MaskSensitiveData(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // Mask Emails
        if (value.Contains("@"))
            return Regex.Replace(value, @"(?<=.{2}).(?=[^@]*?@)", "*");

        return value;
    }

    public override void OnEnd(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToList()) // Clone the list to modify it
        {
            string maskedValue = MaskSensitiveData(tag.Value?.ToString() ?? string.Empty);
            activity.SetTag(tag.Key, maskedValue); // Overwrite the existing tag
        }
    }


}
