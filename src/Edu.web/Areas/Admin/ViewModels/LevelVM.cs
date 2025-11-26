using System.ComponentModel.DataAnnotations;

namespace Edu.Web.Areas.Admin.ViewModels
{
    // used for Create/Edit binding (admin form)
    public class LevelEditVm
    {
        public int Id { get; set; }           // 0 for Create
        public string NameEn { get; set; } = string.Empty;
        public string NameIt { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public int Order { get; set; } = 0;
    }

    // used for listing in Index (includes localized name)
    public class LevelAdminListItemVm
    {
        public int Id { get; set; }
        public string LocalizedName { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string NameIt { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
