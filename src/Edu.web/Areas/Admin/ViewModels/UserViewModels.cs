using Edu.Domain.Entities;
using Edu.Infrastructure.Helpers;

namespace Edu.Web.Areas.Admin.ViewModels
{
    public class UserRowViewModel
    {
        public string Id { get; set; } = default!;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public string Roles { get; set; } = string.Empty;
        public string TeacherStatus { get; set; } = string.Empty;
        public bool? IsAllowed { get; set; } // null = not a student / unknown
    }
        public class UserIndexViewModel
    {
        public int TotalCount { get; set; }
        public int AdminCount { get; set; }
        public int TeacherCount { get; set; }
        public int StudentCount { get; set; }

        // filters / paging state (bind from querystring)
        public string CurrentRoleFilter { get; set; } = "All";
        public bool ShowDeleted { get; set; } = false;
        public string? Search { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        // the actual page of rows
        public PaginatedList<UserRowViewModel>? Items { get; set; }
    }

    public class CurriculumCheckboxVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? LevelName { get; set; }
    }


    public class UserDetailsViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? GuardianPhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? PhotoStorageKey { get; set; }
        public string? PhotoUrl { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();

        // Fix: Ensure Teacher and Student are referenced as types, not namespaces.
        public Domain.Entities.Teacher? Teacher { get; set; }
        public Domain.Entities.Student? Student { get; set; }
        public bool? StudentIsAllowed { get; set; } // new

        // New: all curricula (for the checkbox list)
        public List<CurriculumCheckboxVm> Curricula { get; set; } = new List<CurriculumCheckboxVm>();

        // New: the curriculum ids the student currently has access to
        public List<int> SelectedCurriculumIds { get; set; } = new List<int>();
    }
}
