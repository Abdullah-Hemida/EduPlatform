
namespace Edu.Web.Areas.Teacher.ViewModels
{
    public class SlotListItemVm
    {
        public int Id { get; set; }
        public DateTime StartLocal { get; set; }
        public DateTime EndLocal { get; set; }
        public int Capacity { get; set; }
        public int AvailableSeats { get; set; }
        public decimal Price { get; set; }
        public string? PriceLabel { get; set; }
        public string? LocationUrl { get; set; }
    }

    public class SlotCreateEditVm
    {
        public int Id { get; set; }
        public DateTime StartLocal { get; set; }
        public DateTime EndLocal { get; set; }
        public int Capacity { get; set; } = 1;
        public decimal Price { get; set; }
        public string? PriceLabel { get; set; }
        public string? LocationUrl { get; set; }

        // for concurrency
        public byte[]? RowVersion { get; set; }
    }
}


