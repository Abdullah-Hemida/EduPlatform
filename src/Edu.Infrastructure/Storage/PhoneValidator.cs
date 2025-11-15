using System.Text.RegularExpressions;

namespace Edu.Infrastructure.Storage
{
    public static class PhoneValidator
    {
        // Very simple phone regex: allow + and digits and spaces, min 7 digits
        private static readonly Regex PhoneRegex = new Regex(@"^[+]?([0-9 ]{7,25})$", RegexOptions.Compiled);

        public static bool IsValid(string ContactNumber)
        {
            if (string.IsNullOrWhiteSpace(ContactNumber)) return false;
            return PhoneRegex.IsMatch(ContactNumber.Trim());
        }
    }
}
