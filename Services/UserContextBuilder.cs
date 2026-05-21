using KerzelPay.Data;
using KerzelPay.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace KerzelPay.Services
{
    public class UserContextBuilder
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserContextBuilder(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        /// <summary>
        /// Build a snapshot of the user's account state to give the chatbot context.
        /// Returns null if user isn't logged in.
        /// </summary>
        public async Task<string?> BuildContextAsync(System.Security.Claims.ClaimsPrincipal? principal)
        {
            if (principal?.Identity?.IsAuthenticated != true) return null;

            var user = await _userManager.GetUserAsync(principal);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);

            var accounts = await _db.Accounts
                .Include(a => a.Currency)
                .Where(a => a.UserId == user.Id)
                .ToListAsync();

            var recentTransfers = await _db.Transfers
                .Include(t => t.SourceAccount).ThenInclude(a => a!.Currency)
                .Include(t => t.DestinationAccount).ThenInclude(a => a!.Currency)
                .Where(t =>
                    (t.SourceAccount != null && t.SourceAccount.UserId == user.Id) ||
                    (t.DestinationAccount != null && t.DestinationAccount.UserId == user.Id))
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            var pendingOmtCount = await _db.Transfers
                .CountAsync(t => t.SourceAccount != null
                              && t.SourceAccount.UserId == user.Id
                              && t.Type == TransferType.MobileOmt
                              && t.Status == TransferStatus.Pending);

            var unreadNotifications = await _db.Notifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            var beneficiaryCount = await _db.Beneficiaries
                .CountAsync(b => b.UserId == user.Id);

            // Build a clean text block for Gemini
            var sb = new StringBuilder();
            sb.AppendLine("=== CURRENT USER CONTEXT ===");
            sb.AppendLine($"Name: {user.FirstName} {user.LastName}");
            sb.AppendLine($"Email: {user.Email}");
            sb.AppendLine($"Roles: {string.Join(", ", roles)}");
            sb.AppendLine($"Member since: {user.CreatedAt:dd MMM yyyy}");
            sb.AppendLine();

            sb.AppendLine($"ACCOUNTS ({accounts.Count}):");
            if (accounts.Count == 0)
            {
                sb.AppendLine("  (no accounts yet)");
            }
            else
            {
                foreach (var a in accounts)
                {
                    sb.AppendLine($"  - {a.SerialNumber}: {a.Currency.Symbol}{a.Balance:N2} {a.Currency.Code}");
                }
            }
            sb.AppendLine();

            sb.AppendLine($"RECENT TRANSFERS (last {recentTransfers.Count}):");
            if (recentTransfers.Count == 0)
            {
                sb.AppendLine("  (no transfers yet)");
            }
            else
            {
                foreach (var t in recentTransfers)
                {
                    var direction = t.SourceAccount?.UserId == user.Id ? "SENT" : "RECEIVED";
                    var counterparty = direction == "SENT"
                        ? (t.DestinationAccount?.SerialNumber ?? $"{t.RecipientName} ({t.RecipientMobile})")
                        : t.SourceAccount?.SerialNumber ?? "External";

                    sb.AppendLine($"  - [{t.CreatedAt:dd MMM}] {direction} {t.SourceCurrencyCode} " +
              $"{t.Amount:N2} | {(direction == "SENT" ? "to" : "from")} {counterparty} | " +
              $"Status: {t.Status} | Tracking: {t.TrackingNumber}");
                }
            }
            sb.AppendLine();

            sb.AppendLine($"Pending OMT pickups (sent, awaiting collection): {pendingOmtCount}");
            sb.AppendLine($"Unread notifications: {unreadNotifications}");
            sb.AppendLine($"Saved beneficiaries: {beneficiaryCount}");

            sb.AppendLine();
            sb.AppendLine("INSTRUCTIONS FOR USING THIS DATA:");
            sb.AppendLine("- Refer to the user by their first name when greeting.");
            sb.AppendLine("- When asked about balances or transfers, use ONLY the data above.");
            sb.AppendLine("- If asked about something you don't see here, say 'I don't see that in your recent activity.'");
            sb.AppendLine("- Never reveal account serial numbers of OTHER users, only this user's own data.");
            sb.AppendLine("- For privacy, do not share their email back to them unless asked.");

            return sb.ToString();
        }
    }
}