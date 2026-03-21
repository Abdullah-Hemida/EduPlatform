// Areas/Teacher/Controllers/TeacherBaseController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Policy = "TeacherApproved")]
    public abstract class TeacherBaseController : Controller
    {
    }
}
