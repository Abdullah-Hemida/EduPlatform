namespace Edu.Web.ViewModels
{
    public class PrivateCourseIndexVm
    {
        public string? Query { get; set; }
        public int? SelectedCategoryId { get; set; }
        public List<CategoryVm> Categories { get; set; } = new();
        public List<PrivateCourseListItemVm> Courses { get; set; } = new();
    }

    public class CategoryVm
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class PrivateCourseListItemVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverPublicUrl { get; set; }
        public string? TeacherName { get; set; }
        public string? CategoryName { get; set; }
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
        public string? PriceLabel { get; set; }
        public bool IsPurchased { get; set; } = false;
        public bool HasPendingPurchase { get; set; } = false;
    }
}


