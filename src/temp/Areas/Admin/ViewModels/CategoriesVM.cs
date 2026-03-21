namespace Edu.Web.Areas.Admin.ViewModels
{
    // Edu.Web.Areas.Admin.ViewModels/CategoryAdminListItemVm.cs
    public class CategoryAdminListItemVm
    {
        public int Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string NameIt { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string LocalizedName { get; set; } = string.Empty;
    }

    // Edu.Web.Areas.Admin.ViewModels/CategoryEditVm.cs
    public class CategoryEditVm
    {
        public int Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string NameIt { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
    }

}
