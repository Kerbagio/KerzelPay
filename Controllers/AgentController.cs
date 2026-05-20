using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Models;
using KerzelPay.Repositories;
using KerzelPay.Services;
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

        private readonly AgentCashService _cashService;

        public AgentController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IRepository<Agent> agentRepo,
            AgentCashService cashService)
        {
            _db = db;
            _userManager = userManager;
            _agentRepo = agentRepo;
            _cashService = cashService;
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


        // ===== CASH-IN =====

        // GET: /Agent/CashIn
        [Authorize(Roles = Roles.Agent)]
        public IActionResult CashIn()
        {
            return View(new CashInViewModel());
        }

        // POST: /Agent/CashIn
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Agent)]
        public async Task<IActionResult> CashIn(CashInViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var userId = _userManager.GetUserId(User);
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.UserId == userId);
            if (agent == null) return RedirectToAction(nameof(Status));

            var result = await _cashService.CashInAsync(
                agent.Id,
                vm.CustomerAccountSerial.Trim().ToUpper(),
                vm.Amount,
                vm.Note);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cash-in failed.");
                return View(vm);
            }

            TempData["Success"] = $"Cash-in completed! Tracking: {result.Transfer!.TrackingNumber}. " +
                                  $"Your commission: ${result.Commission:N2}";
            return RedirectToAction(nameof(Dashboard));
        }

        // ===== CASH-OUT (OMT pickup) =====

        // GET: /Agent/CashOut
        [Authorize(Roles = Roles.Agent)]
        public IActionResult CashOut()
        {
            return View(new CashOutViewModel());
        }

        // GET: /Agent/CashOutLookup?tracking=TRX-...
        [Authorize(Roles = Roles.Agent)]
        public async Task<IActionResult> CashOutLookup(string tracking)
        {
            if (string.IsNullOrWhiteSpace(tracking))
            {
                TempData["Error"] = "Please enter a tracking number.";
                return RedirectToAction(nameof(CashOut));
            }

            var transfer = await _db.Transfers
                .FirstOrDefaultAsync(t => t.TrackingNumber == tracking.Trim());

            if (transfer == null)
            {
                TempData["Error"] = "Transfer not found.";
                return RedirectToAction(nameof(CashOut));
            }

            if (transfer.Type != TransferType.MobileOmt)
            {
                TempData["Error"] = "Only OMT mobile transfers can be cashed out.";
                return RedirectToAction(nameof(CashOut));
            }

            if (transfer.Status != TransferStatus.Pending)
            {
                TempData["Error"] = $"This transfer is already {transfer.Status}. Cannot cash out.";
                return RedirectToAction(nameof(CashOut));
            }

            ViewBag.Transfer = transfer;
            return View("CashOutConfirm", new CashOutViewModel
            {
                TrackingNumber = transfer.TrackingNumber
            });
        }

        // POST: /Agent/CashOut
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Agent)]
        public async Task<IActionResult> CashOut(CashOutViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                // Need to reload the transfer for the confirmation view
                var t = await _db.Transfers
                    .FirstOrDefaultAsync(t => t.TrackingNumber == vm.TrackingNumber);
                ViewBag.Transfer = t;
                return View("CashOutConfirm", vm);
            }

            var userId = _userManager.GetUserId(User);
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.UserId == userId);
            if (agent == null) return RedirectToAction(nameof(Status));

            var result = await _cashService.CashOutAsync(
                agent.Id,
                vm.TrackingNumber.Trim(),
                vm.RecipientIdProof.Trim());

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cash-out failed.");
                var t = await _db.Transfers.FirstOrDefaultAsync(t => t.TrackingNumber == vm.TrackingNumber);
                ViewBag.Transfer = t;
                return View("CashOutConfirm", vm);
            }

            TempData["Success"] = $"Cash-out completed! Tracking: {result.Transfer!.TrackingNumber}. " +
                                  $"Your commission: ${result.Commission:N2}. Please hand over the cash.";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /Agent/PendingPickups — list all OMT transfers awaiting cash-out
        [Authorize(Roles = Roles.Agent)]
        public async Task<IActionResult> PendingPickups()
        {
            var pending = await _db.Transfers
                .Include(t => t.SourceAccount)
                .Where(t => t.Type == TransferType.MobileOmt && t.Status == TransferStatus.Pending)
                .OrderByDescending(t => t.CreatedAt)
                .Take(50)
                .ToListAsync();

            return View(pending);
        }
    }
}