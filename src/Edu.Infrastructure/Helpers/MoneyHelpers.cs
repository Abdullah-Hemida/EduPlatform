
using System.Globalization;

namespace Edu.Infrastructure.Helpers
{
    public static class MoneyHelpers
    {
        private static readonly CultureInfo EuroCulture = new("fr-FR");
        // "fr-FR" or "de-DE" both use Euro; pick one based on your localization preference

        /// <summary>
        /// Format a decimal value as Euro currency with proper localization.
        /// </summary>
        public static string ToEuro(this decimal amount)
        {
            return string.Format(EuroCulture, "{0:C}", amount);
        }

        /// <summary>
        /// Format a decimal? value as Euro, handling null safely.
        /// </summary>
        public static string ToEuro(this decimal? amount)
        {
            if (amount.HasValue)
                return string.Format(EuroCulture, "{0:C}", amount.Value);
            return "€0.00";
        }

        /// <summary>
        /// Parses a Euro-formatted string back into decimal (if needed).
        /// </summary>
        public static decimal ParseEuro(string euroString)
        {
            if (decimal.TryParse(
                    euroString,
                    NumberStyles.Currency,
                    EuroCulture,
                    out decimal result))
            {
                return result;
            }
            throw new FormatException("Invalid Euro currency string.");
        }
    }
}

