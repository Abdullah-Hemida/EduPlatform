// TeacherApprovedRequirement.cs
using Microsoft.AspNetCore.Authorization;
namespace Edu.Infrastructure.Helpers
{
    public class TeacherApprovedRequirement : IAuthorizationRequirement
    {
        public TeacherApprovedRequirement() { }
    }
}
