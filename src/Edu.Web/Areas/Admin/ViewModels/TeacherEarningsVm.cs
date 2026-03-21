using System.Globalization;

namespace Edu.Web.Areas.Admin.ViewModels
{
    public class EarningsIndexVm
    {
        public int Year { get; set; }
        public List<TeacherEarningsVm> Teachers { get; set; } = new();
    }

    public class TeacherEarningsVm
    {
        public string TeacherId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? PhotoUrl { get; set; }
        public string? PhoneNumber { get; set; }

        // 12 months: index 0 => Jan
        public decimal[] Months { get; set; } = new decimal[12];

        // computed total across months
        public decimal Total { get; set; } = 0m;

        // convenience: format amount using Italian euro formatting
        public string FormatMoney(decimal v)
        {
            var ci = CultureInfo.GetCultureInfo("it-IT");
            return v.ToString("C", ci); // e.g. "€ 1.234,56"
        }
    }
}

