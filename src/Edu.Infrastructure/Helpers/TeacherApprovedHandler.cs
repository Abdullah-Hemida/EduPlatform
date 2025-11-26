
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Edu.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Edu.Infrastructure.Helpers
{
    public class TeacherApprovedHandler : AuthorizationHandler<TeacherApprovedRequirement>
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<TeacherApprovedHandler> _logger;

        public TeacherApprovedHandler(ApplicationDbContext db, ILogger<TeacherApprovedHandler> logger)
        {
            _db = db;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TeacherApprovedRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            // get user id from claims
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("TeacherApprovedHandler: no user id in claims.");
                return;
            }

            try
            {
                var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == userId);
                if (teacher != null && teacher.Status == Edu.Domain.Entities.TeacherStatus.Approved)
                {
                    context.Succeed(requirement);
                }
                else
                {
                    // not approved or not found -> do nothing (forbidden)
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking teacher status for user {UserId}", userId);
                // keep silent (do not succeed)
            }
        }
    }
}
