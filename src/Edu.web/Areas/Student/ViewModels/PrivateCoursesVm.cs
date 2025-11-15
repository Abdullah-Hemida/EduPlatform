using System.ComponentModel.DataAnnotations;
using Edu.Web.Resources;
using Edu.Domain.Entities;

namespace Edu.Web.Areas.Student.ViewModels
{
    public class PrivateCourseListItemVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        public string? CoverPublicUrl { get; set; }
        public string? TeacherName { get; set; }
        public string? CategoryName { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
    }

    public class PrivateCourseDetailsVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverPublicUrl { get; set; }
        public string? TeacherName { get; set; }
        public string? CategoryName { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
        public bool IsPublished { get; set; }
        public bool IsPurchased { get; set; } // whether current student has completed purchase
        public int TotalLessons { get; set; }
        public List<PrivateModuleVm> Modules { get; set; } = new();
    }

    public class PrivateModuleVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public List<PrivateLessonVm> Lessons { get; set; } = new();
    }

    public class PrivateLessonVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? YouTubeVideoId { get; set; }   // extracted id if available
        public string? VideoUrl { get; set; }         // fallback direct url
        public List<FileResourceVm> Files { get; set; } = new();
    }

    public class FileResourceVm
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? PublicUrl { get; set; }
        public string? FileType { get; set; }
    }

    public class PurchaseRequestCreateVm
    {
        public int PrivateCourseId { get; set; }
        public string? Note { get; set; }
    }

    public class MyPurchasesVm
    {
        public List<MyPurchaseItemVm> Purchases { get; set; } = new();
    }

    public class MyPurchaseItemVm
    {
        public int Id { get; set; } // PurchaseRequest id
        public int PrivateCourseId { get; set; }
        public string CourseTitle { get; set; } = null!;
        public DateTime RequestDateUtc { get; set; }
        public PurchaseStatus Status { get; set; }
        public string? TeacherName { get; set; }
        public string? CoverPublicUrl { get; set; }
    }
}

