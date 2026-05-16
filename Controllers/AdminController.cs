using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET: /Admin/Dashboard — temporary, full version in Session 10
        public async Task<IActionResult> Dashboard()
        {
            var pendingAgents = await _db.Agents
                .Include(a => a.User)
                .Where(a => a.Status == AgentStatus.Pending)
                .ToListAsync();

            return View(pendingAgents);
        }

        // POST: /Admin/ApproveAgent/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAgent(int id)
        {
            var agent = await _db.Agents
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (agent == null) return NotFound();

            agent.Status = AgentStatus.Approved;

            // Grant the Agent role
            if (agent.User != null && !await _userManager.IsInRoleAsync(agent.User, Roles.Agent))
            {
                await _userManager.AddToRoleAsync(agent.User, Roles.Agent);
            }

            // Notify the user in-app
            _db.Notifications.Add(new Notification
            {
                UserId = agent.UserId,
                Title = "Agent application approved 🎉",
                Message = $"Welcome to the Kerzel Pay agent network! You can now access the Agent Dashboard."
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{agent.StoreName} approved.";
            return RedirectToAction(nameof(Dashboard));
        }

        // POST: /Admin/RejectAgent/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectAgent(int id)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == id);
            if (agent == null) return NotFound();

            agent.Status = AgentStatus.Rejected;

            _db.Notifications.Add(new Notification
            {
                UserId = agent.UserId,
                Title = "Agent application rejected",
                Message = "Unfortunately, your application was not approved. Please contact support for details."
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{agent.StoreName} rejected.";
            return RedirectToAction(nameof(Dashboard));
        }
    }
}