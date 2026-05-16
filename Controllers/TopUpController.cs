using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Helpers;
using KerzelPay.Models;
using KerzelPay.Services;
using KerzelPay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Controllers
{
    [Authorize(Roles = Roles.User)]
    public class TopUpController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly StripeService _stripeService;
        private readonly IEmailService _emailService;

        public TopUpController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        StripeService stripeService,
        IEmailService emailService)
        {
            _db = db;
            _userManager = userManager;
            _stripeService = stripeService;
            _emailService = emailService;
        }

        // GET: /TopUp/Index/5  (the 5 = account id)
        public async Task<IActionResult> Index(int id)
        {
            var userId = _userManager.GetUserId(User);

            var account = await _db.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null) return NotFound();

            var vm = new TopUpViewModel
            {
                AccountId = account.Id,
                AccountSerialNumber = account.SerialNumber,
                CurrencyCode = account.Currency.Code,
                CurrencySymbol = account.Currency.Symbol
            };

            return View(vm);
        }

        // POST: /TopUp/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(TopUpViewModel vm)
        {
            var userId = _userManager.GetUserId(User);

            var account = await _db.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == vm.AccountId && a.UserId == userId);

            if (account == null) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.AccountSerialNumber = account.SerialNumber;
                vm.CurrencyCode = account.Currency.Code;
                vm.CurrencySymbol = account.Currency.Symbol;
                return View("Index", vm);
            }

            // LBP isn't supported by Stripe Checkout — block it nicely
            if (account.Currency.Code == "LBP")
            {
                TempData["Error"] = "LBP top-ups are only available through agents. Please visit a partner store.";
                return RedirectToAction("Index", "Account");
            }

            // Build success & cancel URLs
            var successUrl = Url.Action("Success", "TopUp", null, Request.Scheme)!;
            var cancelUrl = Url.Action("Cancel", "TopUp", new { id = account.Id }, Request.Scheme)!;

            var session = await _stripeService.CreateCheckoutSessionAsync(
                vm.Amount,
                account.Currency.Code,
                account.Id,
                successUrl,
                cancelUrl);

            // Redirect to Stripe's hosted checkout page
            return Redirect(session.Url);
        }

        // GET: /TopUp/Success?session_id=cs_test_...
        public async Task<IActionResult> Success(string session_id)
        {
            if (string.IsNullOrEmpty(session_id))
            {
                TempData["Error"] = "Invalid payment session.";
                return RedirectToAction("Index", "Account");
            }

            var session = await _stripeService.GetSessionAsync(session_id);

            // Verify the payment actually succeeded
            if (session.PaymentStatus != "paid")
            {
                TempData["Error"] = "Payment was not completed.";
                return RedirectToAction("Index", "Account");
            }

            // Idempotency check: if we already processed this session, don't double-credit
            var alreadyProcessed = await _db.Transfers
                .AnyAsync(t => t.Note != null && t.Note.Contains(session_id));

            if (alreadyProcessed)
            {
                TempData["Success"] = "This top-up has already been processed.";
                return RedirectToAction("Index", "Account");
            }

            // Read metadata
            var accountId = int.Parse(session.Metadata["account_id"]);
            var originalAmount = decimal.Parse(session.Metadata["original_amount"]);
            var currencyCode = session.Metadata["currency_code"];

            var userId = _userManager.GetUserId(User);

            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return NotFound();

            // Credit the account
            account.Balance += originalAmount;

            // Record as a Transfer (Type = TopUp) for history
            var topUp = new Transfer
            {
                TrackingNumber = SerialNumberGenerator.GenerateTransferTrackingNumber(),
                Type = TransferType.TopUp,
                Status = TransferStatus.Completed,
                DestinationAccountId = account.Id,
                Amount = originalAmount,
                ConvertedAmount = originalAmount,
                ExchangeRate = 1m,
                Commission = 0m,
                SourceCurrencyCode = currencyCode,
                DestinationCurrencyCode = currencyCode,
                Note = $"Stripe top-up | session: {session_id}",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            _db.Transfers.Add(topUp);
            await _db.SaveChangesAsync();

            // Send confirmation email
            var user = await _userManager.GetUserAsync(User);
            if (user?.Email != null)
            {
                await _emailService.SendAsync(
                    user.Email,
                    $"Top-up successful — {topUp.TrackingNumber}",
                    EmailTemplates.TopUpSuccess(topUp));
            }

            TempData["Success"] = $"Top-up successful! {currencyCode} {originalAmount:N2} added to your account.";
            return View(topUp);
        }

        // GET: /TopUp/Cancel/5
        public IActionResult Cancel(int id)
        {
            TempData["Error"] = "Top-up cancelled.";
            return RedirectToAction("Index", new { id });
        }
    }
}