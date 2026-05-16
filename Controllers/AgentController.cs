using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Models;
using KerzelPay.Repositories;
using KerzelPay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Controllers
{
    public class AgentController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRepository<Agent> _agentRepo;

        public AgentController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IRepository<Agent> agentRepo)
        {
            _db = db;
            _userManager = userManager;
            _agentRepo = agentRepo;
        }

        // GET: /Agent/Map — PUBLIC map of all approved agents
        [AllowAnonymous]
        public async Task<IActionResult> Map()
        {
            var agents = await _db.Agents
                .Include(a => a.User)
                .Where(a => a.Status == AgentStatus.Approved)
                .ToListAsync();

            return View(agents);
        }

        // GET: /Agent/Apply — form for a user to apply
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> Apply()
        {
            var userId = _userManager.GetUserId(User);

            // Has this user already applied?
            var existing = await _db.Agents
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (existing != null)
            {
                return RedirectToAction(nameof(Status));
            }

            return View(new AgentApplicationViewModel());
        }

        // POST: /Agent/Apply
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> Apply(AgentApplicationViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var userId = _userManager.GetUserId(User)!;

            // Prevent double applications
            var existing = await _db.Agents.AnyAsync(a => a.UserId == userId);
            if (existing)
            {
                TempData["Error"] = "You've already applied to be an agent.";
                return RedirectToAction(nameof(Status));
            }

            var agent = new Agent
            {
                UserId = userId,
                StoreName = vm.StoreName.Trim(),
                Address = vm.Address.Trim(),
                Latitude = vm.Latitude,
                Longitude = vm.Longitude,
                WorkingHours = vm.FormatWorkingHours(),
                Status = AgentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _agentRepo.AddAsync(agent);

            // In-app notification
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Agent application submitted",
                Message = "Your application is under review. We'll notify you once it's approved."
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Application submitted! Admin will review it shortly.";
            return RedirectToAction(nameof(Status));
        }

        // GET: /Agent/Status — see own application status
        [Authorize]
        public async Task<IActionResult> Status()
        {
            var userId = _userManager.GetUserId(User);

            var agent = await _db.Agents
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (agent == null)
            {
                return RedirectToAction(nameof(Apply));
            }

            return View(agent);
        }

        // GET: /Agent/Dashboard — for approved agents
        [Authorize(Roles = Roles.Agent)]
        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);

            var agent = await _db.Agents
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (agent == null || agent.Status != AgentStatus.Approved)
            {
                return RedirectToAction(nameof(Status));
            }

            // Recent transactions processed by this agent
            var recentTransactions = await _db.Transfers
                .Include(t => t.SourceAccount)
                .Where(t => t.AgentId == agent.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .ToListAsync();

            // Stats
            ViewBag.TotalTransactions = await _db.Transfers
                .CountAsync(t => t.AgentId == agent.Id);

            ViewBag.TotalCommission = agent.TotalCommission;

            ViewBag.RecentTransactions = recentTransactions;

            return View(agent);
        }
    }
}