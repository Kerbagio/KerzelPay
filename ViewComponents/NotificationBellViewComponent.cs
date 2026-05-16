using KerzelPay.Data;
using KerzelPay.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.ViewComponents
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationBellViewComponent(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Get current user safely (View Components don't auto-inject User the same way)
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null) return View(new NotificationBellModel());

            var recent = await _db.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync();

            var unreadCount = await _db.Notifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            var model = new NotificationBellModel
            {
                Recent = recent,
                UnreadCount = unreadCount
            };

            return View(model);
        }
    }

    public class NotificationBellModel
    {
        public List<Notification> Recent { get; set; } = new();
        public int UnreadCount { get; set; }
    }
}