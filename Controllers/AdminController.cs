using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Models;
using KerzelPay.Services;
using KerzelPay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace KerzelPay.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RateRefreshService _rateRefreshService;
        private readonly string _superAdminEmail;

        public AdminController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RateRefreshService rateRefreshService,
        IConfiguration configuration)
        {
            _db = db;
            _userManager = userManager;
            _rateRefreshService = rateRefreshService;
            _superAdminEmail = configuration["SuperAdmin:Email"] ?? "admin@kerzelpay.com";
        }

        // GET: /Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var totalUsers = await _userManager.Users.CountAsync();
            var totalAgents = await _db.Agents.CountAsync(a => a.Status == AgentStatus.Approved);
            var pendingAgents = await _db.Agents.CountAsync(a => a.Status == AgentStatus.Pending);
            var totalTransfers = await _db.Transfers.CountAsync();
            var totalCommission = await _db.Transfers.SumAsync(t => (decimal?)t.Commission) ?? 0m;

            // Last 30 days of transfers (for chart)
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30).Date;
            var dailyTransfers = await _db.Transfers
                .Where(t => t.CreatedAt >= thirtyDaysAgo)
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();

            // Currency breakdown for pie chart
            var currencyBreakdown = await _db.Transfers
                .GroupBy(t => t.SourceCurrencyCode)
                .Select(g => new { Currency = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalAgents = totalAgents;
            ViewBag.PendingAgents = pendingAgents;
            ViewBag.TotalTransfers = totalTransfers;
            ViewBag.TotalCommission = totalCommission;
            ViewBag.DailyTransfersJson = System.Text.Json.JsonSerializer.Serialize(
                dailyTransfers.Select(d => new { date = d.Date.ToString("yyyy-MM-dd"), count = d.Count }));
            ViewBag.CurrencyBreakdownJson = System.Text.Json.JsonSerializer.Serialize(currencyBreakdown);

            return View();
        }

        // ===== AGENTS =====

        public async Task<IActionResult> Agents(string? status)
        {
            var query = _db.Agents.Include(a => a.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AgentStatus>(status, true, out var s))
            {
                query = query.Where(a => a.Status == s);
            }

            var agents = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
            ViewBag.Status = status;
            return View(agents);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAgent(int id)
        {
            var agent = await _db.Agents.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == id);
            if (agent == null) return NotFound();

            agent.Status = AgentStatus.Approved;
            if (agent.User != null && !await _userManager.IsInRoleAsync(agent.User, Roles.Agent))
                await _userManager.AddToRoleAsync(agent.User, Roles.Agent);

            _db.Notifications.Add(new Notification
            {
                UserId = agent.UserId,
                Title = "Agent application approved 🎉",
                Message = "You can now access the Agent Dashboard."
            });
            agent.IsResubmission = false;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{agent.StoreName} approved.";
            return RedirectToAction(nameof(Agents));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectAgent(int id)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == id);
            if (agent == null) return NotFound();

            agent.Status = AgentStatus.Rejected;
            _db.Notifications.Add(new Notification
            {
                UserId = agent.UserId,
                Title = "Agent application rejected",
                Message = "Please contact support for more details."
            });
            agent.IsResubmission = false;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{agent.StoreName} rejected.";
            return RedirectToAction(nameof(Agents));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendAgent(int id)
        {
            var agent = await _db.Agents.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == id);
            if (agent == null) return NotFound();

            agent.Status = AgentStatus.Suspended;
            if (agent.User != null && await _userManager.IsInRoleAsync(agent.User, Roles.Agent))
                await _userManager.RemoveFromRoleAsync(agent.User, Roles.Agent);

            await _db.SaveChangesAsync();
            TempData["Success"] = $"{agent.StoreName} suspended.";
            return RedirectToAction(nameof(Agents));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UnsuspendAgent(int id)
        {
            var agent = await _db.Agents
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (agent == null) return NotFound();

            if (agent.Status != AgentStatus.Suspended)
            {
                TempData["Error"] = "Only suspended agents can be reactivated.";
                return RedirectToAction(nameof(Agents));
            }

            // Reactivate the agent
            agent.Status = AgentStatus.Approved;

            // Restore the Agent role
            if (agent.User != null && !await _userManager.IsInRoleAsync(agent.User, Roles.Agent))
            {
                await _userManager.AddToRoleAsync(agent.User, Roles.Agent);
            }

            // Notify the agent
            _db.Notifications.Add(new Notification
            {
                UserId = agent.UserId,
                Title = "Agent account reactivated ✅",
                Message = "Your suspension has been lifted. You can access the Agent Dashboard again."
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{agent.StoreName} reactivated.";
            return RedirectToAction(nameof(Agents));
        }

        // ===== CURRENCIES =====

        public async Task<IActionResult> Currencies()
        {
            var currencies = await _db.Currencies.OrderBy(c => c.Code).ToListAsync();
            return View(currencies);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCurrency(int id, decimal rate, bool isActive)
        {
            var currency = await _db.Currencies.FindAsync(id);
            if (currency == null) return NotFound();

            if (rate <= 0)
            {
                TempData["Error"] = "Rate must be greater than 0.";
                return RedirectToAction(nameof(Currencies));
            }

            currency.ExchangeRateToUsd = rate;
            currency.IsActive = isActive;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"{currency.Code} updated.";
            return RedirectToAction(nameof(Currencies));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshRates()
        {
            var result = await _rateRefreshService.RefreshRatesAsync();

            if (result.IsSuccess)
            {
                TempData["Success"] = $"Refreshed {result.UpdatedCount} currency rates from Frankfurter API.";
            }
            else
            {
                TempData["Error"] = $"Could not refresh rates: {result.ErrorMessage}. Cached rates still active.";
            }

            return RedirectToAction(nameof(Currencies));
        }

        // ===== SETTINGS =====

        public async Task<IActionResult> Settings()
        {
            var commission = await _db.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "CommissionPercent");
            var agentCommission = await _db.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "AgentCommissionPercent");

            ViewBag.CommissionPercent = commission?.Value ?? "1.0";
            ViewBag.AgentCommissionPercent = agentCommission?.Value ?? "0.5";
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCommission(decimal commissionPercent)
        {
            if (commissionPercent < 0 || commissionPercent > 50)
            {
                TempData["Error"] = "Commission must be between 0% and 50%.";
                return RedirectToAction(nameof(Settings));
            }

            var setting = await _db.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "CommissionPercent");

            if (setting == null)
            {
                setting = new AppSetting { Key = "CommissionPercent" };
                _db.AppSettings.Add(setting);
            }

            setting.Value = commissionPercent.ToString("F2");
            setting.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Commission rate updated to {commissionPercent:F2}%.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAgentCommission(decimal agentCommissionPercent)
        {
            if (agentCommissionPercent < 0 || agentCommissionPercent > 50)
            {
                TempData["Error"] = "Agent commission must be between 0% and 50%.";
                return RedirectToAction(nameof(Settings));
            }

            var setting = await _db.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "AgentCommissionPercent");

            if (setting == null)
            {
                setting = new AppSetting { Key = "AgentCommissionPercent" };
                _db.AppSettings.Add(setting);
            }

            setting.Value = agentCommissionPercent.ToString("F2");
            setting.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Agent commission rate updated to {agentCommissionPercent:F2}%.";
            return RedirectToAction(nameof(Settings));
        }

        // ===== USERS =====

        public async Task<IActionResult> Users(string? q, string? role)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(u =>
                    u.Email!.Contains(q) ||
                    u.FirstName.Contains(q) ||
                    u.LastName.Contains(q));
            }

            var users = await query.OrderBy(u => u.Email).Take(200).ToListAsync();

            // Build user → roles map
            var userRoles = new Dictionary<string, IList<string>>();
            foreach (var u in users)
            {
                userRoles[u.Id] = await _userManager.GetRolesAsync(u);
            }

            // Optional role filter (in-memory because roles aren't a simple FK)
            if (!string.IsNullOrWhiteSpace(role))
            {
                users = users.Where(u => userRoles[u.Id].Contains(role)).ToList();
            }

            ViewBag.UserRoles = userRoles;
            ViewBag.Query = q;
            ViewBag.RoleFilter = role;
            ViewBag.SuperAdminEmail = _superAdminEmail;
            return View(users);
        }

        // ===== TRANSFERS =====

        public async Task<IActionResult> Transfers(string? q, string? type, string? status)
        {
            var query = _db.Transfers
                .Include(t => t.SourceAccount).ThenInclude(a => a!.Currency)
                .Include(t => t.DestinationAccount).ThenInclude(a => a!.Currency)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(t => t.TrackingNumber.Contains(q));
            }

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<TransferType>(type, true, out var t))
                query = query.Where(x => x.Type == t);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TransferStatus>(status, true, out var st))
                query = query.Where(x => x.Status == st);

            var transfers = await query
                .OrderByDescending(t => t.CreatedAt)
                .Take(200)
                .ToListAsync();

            ViewBag.Query = q;
            ViewBag.TypeFilter = type;
            ViewBag.StatusFilter = status;
            return View(transfers);
        }

        // ===== EXPORTS =====

        public async Task<IActionResult> ExportTransfers()
        {
            var transfers = await _db.Transfers
                .Include(t => t.SourceAccount)
                .Include(t => t.DestinationAccount)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("TrackingNumber,Type,Status,SourceAccount,DestinationAccount,Amount,SourceCurrency,ConvertedAmount,DestinationCurrency,ExchangeRate,Commission,CreatedAt");

            foreach (var t in transfers)
            {
                csv.AppendLine(string.Join(",",
                    t.TrackingNumber,
                    t.Type,
                    t.Status,
                    t.SourceAccount?.SerialNumber ?? "",
                    t.DestinationAccount?.SerialNumber ?? t.RecipientMobile ?? "",
                    t.Amount.ToString("F2"),
                    t.SourceCurrencyCode,
                    t.ConvertedAmount.ToString("F2"),
                    t.DestinationCurrencyCode,
                    t.ExchangeRate.ToString("F6"),
                    t.Commission.ToString("F2"),
                    t.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"kerzelpay-transfers-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        // POST: /Admin/AddRole
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRole(string userId, string role)
        {
            // Validate the role is one of our known roles
            var validRoles = new[] { Roles.Admin, Roles.Agent, Roles.User };
            if (!validRoles.Contains(role))
            {
                TempData["Error"] = "Invalid role.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Super admin check
            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null) return NotFound();

            // You can still ADD roles to super admin, just not remove them
            // No restriction here — adding is safe

            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId && role == Roles.Admin)
            {
                TempData["Error"] = "You cannot modify your own Admin role.";
                return RedirectToAction(nameof(Users));
            }

            if (!await _userManager.IsInRoleAsync(user, role))
            {
                await _userManager.AddToRoleAsync(user, role);
                TempData["Success"] = $"{user.Email} → {role} role added.";
            }
            else
            {
                TempData["Error"] = $"{user.Email} already has the {role} role.";
            }

            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/RemoveRole
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string userId, string role)
        {
            var validRoles = new[] { Roles.Admin, Roles.Agent, Roles.User };
            if (!validRoles.Contains(role))
            {
                TempData["Error"] = "Invalid role.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Safety: super admin account is fully protected
            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser?.Email?.ToLower() == _superAdminEmail.ToLower())
            {
                TempData["Error"] = "The super admin account cannot be modified.";
                return RedirectToAction(nameof(Users));
            }

            // Safety: prevent removing your own admin role
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId && role == Roles.Admin)
            {
                TempData["Error"] = "You cannot remove your own Admin role.";
                return RedirectToAction(nameof(Users));
            }

            if (await _userManager.IsInRoleAsync(user, role))
            {
                await _userManager.RemoveFromRoleAsync(user, role);

                // If removing Agent role, also suspend their agent record
                if (role == Roles.Agent)
                {
                    var agent = await _db.Agents.FirstOrDefaultAsync(a => a.UserId == userId);
                    if (agent != null && agent.Status == AgentStatus.Approved)
                    {
                        agent.Status = AgentStatus.Suspended;
                        await _db.SaveChangesAsync();
                    }
                }

                TempData["Success"] = $"{user.Email} → {role} role removed.";
            }
            else
            {
                TempData["Error"] = $"{user.Email} doesn't have the {role} role.";
            }

            return RedirectToAction(nameof(Users));
        }


        // GET: /Admin/DeleteUser/{userId} — confirmation page
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Super admin can never be deleted
            if (user.Email?.ToLower() == _superAdminEmail.ToLower())
            {
                TempData["Error"] = "The super admin account cannot be deleted.";
                return RedirectToAction(nameof(Users));
            }

            // Can't delete yourself
            if (id == _userManager.GetUserId(User))
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Users));
            }

            // Collect info for the confirmation screen
            var accounts = await _db.Accounts
                .Include(a => a.Currency)
                .Where(a => a.UserId == id)
                .ToListAsync();

            var totalBalance = accounts.Sum(a => a.Balance);
            var pendingOmtCount = await _db.Transfers
                .CountAsync(t => t.SourceAccount != null
                              && t.SourceAccount.UserId == id
                              && t.Type == TransferType.MobileOmt
                              && t.Status == TransferStatus.Pending);

            var roles = await _userManager.GetRolesAsync(user);

            ViewBag.Accounts = accounts;
            ViewBag.TotalBalance = totalBalance;
            ViewBag.PendingOmtCount = pendingOmtCount;
            ViewBag.Roles = roles;
            ViewBag.HasPendingTransfers = pendingOmtCount > 0;
            ViewBag.HasPositiveBalance = accounts.Any(a => a.Balance > 0);

            return View(user);
        }

        // POST: /Admin/DeleteUser/{userId}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Re-check safety guards
            if (user.Email?.ToLower() == _superAdminEmail.ToLower())
            {
                TempData["Error"] = "The super admin account cannot be deleted.";
                return RedirectToAction(nameof(Users));
            }

            if (id == _userManager.GetUserId(User))
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Users));
            }

            // Refuse if they have money in any account
            var accounts = await _db.Accounts.Where(a => a.UserId == id).ToListAsync();
            if (accounts.Any(a => a.Balance > 0))
            {
                TempData["Error"] = "Cannot delete: user has accounts with positive balance. " +
                                    "They must transfer or cash out the funds first.";
                return RedirectToAction(nameof(Users));
            }

            // Refuse if they have pending OMT transfers (money still in escrow)
            var hasPending = await _db.Transfers.AnyAsync(t =>
                t.SourceAccount != null
                && t.SourceAccount.UserId == id
                && t.Type == TransferType.MobileOmt
                && t.Status == TransferStatus.Pending);

            if (hasPending)
            {
                TempData["Error"] = "Cannot delete: user has pending OMT transfers awaiting pickup.";
                return RedirectToAction(nameof(Users));
            }

            // Begin transactional delete
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1. Unlink transfers (preserve history for OTHER users)
                var accountIds = accounts.Select(a => a.Id).ToList();

                var transfersAsSource = await _db.Transfers
                    .Where(t => t.SourceAccountId != null && accountIds.Contains(t.SourceAccountId.Value))
                    .ToListAsync();
                foreach (var t in transfersAsSource) t.SourceAccountId = null;

                var transfersAsDest = await _db.Transfers
                    .Where(t => t.DestinationAccountId != null && accountIds.Contains(t.DestinationAccountId.Value))
                    .ToListAsync();
                foreach (var t in transfersAsDest) t.DestinationAccountId = null;

                await _db.SaveChangesAsync();

                // 2. Delete user's own data
                _db.Accounts.RemoveRange(accounts);

                var beneficiaries = await _db.Beneficiaries.Where(b => b.UserId == id).ToListAsync();
                _db.Beneficiaries.RemoveRange(beneficiaries);

                var reviews = await _db.Reviews.Where(r => r.UserId == id).ToListAsync();
                _db.Reviews.RemoveRange(reviews);

                var notifications = await _db.Notifications.Where(n => n.UserId == id).ToListAsync();
                _db.Notifications.RemoveRange(notifications);

                // 3. Delete agent record if exists
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.UserId == id);
                if (agent != null) _db.Agents.Remove(agent);

                await _db.SaveChangesAsync();

                // 4. Finally delete the Identity user (roles, logins, claims auto-cascade)
                var email = user.Email ?? "user";
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    await tx.RollbackAsync();
                    TempData["Error"] = "Failed to delete user: " +
                                        string.Join(", ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Users));
                }

                await tx.CommitAsync();
                TempData["Success"] = $"Account {email} deleted successfully.";
                return RedirectToAction(nameof(Users));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["Error"] = $"Deletion failed: {ex.Message}";
                return RedirectToAction(nameof(Users));
            }
        }
    }
}