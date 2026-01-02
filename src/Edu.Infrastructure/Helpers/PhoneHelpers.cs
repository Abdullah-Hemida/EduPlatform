// Helpers/PhoneHelpers.cs
using System.Text.RegularExpressions;
using PhoneNumbers;

public static class PhoneHelpers
{
    // Normalize to E.164 including leading + (e.g. "+393451234567"), or null if invalid.
    public static string? ToE164(string? rawNumber, string defaultRegion = "IT")
    {
        if (string.IsNullOrWhiteSpace(rawNumber)) return null;
        try
        {
            var util = PhoneNumberUtil.GetInstance();
            var pn = util.Parse(rawNumber.Trim(), defaultRegion);
            if (!util.IsValidNumber(pn)) return null;
            return util.Format(pn, PhoneNumberFormat.E164); // includes '+'
        }
        catch
        {
            return null;
        }
    }

    // For WhatsApp links we want digits without '+'; return null if no digits available.
    public static string? ToWhatsappDigits(string? rawNumber, string defaultRegion = "IT")
    {
        if (string.IsNullOrWhiteSpace(rawNumber)) return null;

        // 1) Prefer libphonenumber normalized form
        var e164 = ToE164(rawNumber, defaultRegion);
        if (!string.IsNullOrWhiteSpace(e164))
        {
            return Regex.Replace(e164, @"^\+", ""); // remove leading +
        }

        // 2) Fallback: extract digits from raw input (accept if reasonable length)
        var digits = Regex.Replace(rawNumber ?? "", @"\D", "");
        if (digits.Length >= 6 && digits.Length <= 15)
            return digits;

        return null;
    }
}


