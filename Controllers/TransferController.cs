using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Models;
using KerzelPay.Services;
using KerzelPay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Controllers
{
    [Authorize(Roles = Roles.User)]
    public class TransferController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TransferService _transferService;
        private readonly CurrencyService _currencyService;

        public TransferController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            TransferService transferService,
            CurrencyService currencyService)
        {
            _db = db;
            _userManager = userManager;
            _transferService = transferService;
            _currencyService = currencyService;
        }

        // GET: /Transfer  (history)
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Show transfers where the user is sender OR receiver
            var transfers = await _db.Transfers
                .Include(t => t.SourceAccount).ThenInclude(a => a!.Currency)
                .Include(t => t.DestinationAccount).ThenInclude(a => a!.Currency)
                .Where(t =>
                    (t.SourceAccount != null && t.SourceAccount.UserId == userId) ||
                    (t.DestinationAccount != null && t.DestinationAccount.UserId == userId))
                .OrderByDescending(t => t.CreatedAt)
                .Take(100)
                .ToListAsync();

            ViewBag.CurrentUserId = userId;
            return View(transfers);
        }

        // GET: /Transfer/Create
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View(new TransferViewModel());
        }

        // POST: /Transfer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TransferViewModel vm)
        {
            var userId = _userManager.GetUserId(User)!;

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(vm);
            }

            // Verify source account belongs to current user
            var sourceAccount = await _db.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == vm.SourceAccountId && a.UserId == userId);

            if (sourceAccount == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid source account.");
                await PopulateDropdownsAsync();
                return View(vm);
            }

            TransferResult result;

            switch (vm.Mode)
            {
                case TransferMode.Beneficiary:
                    var beneficiary = await _db.Beneficiaries
                        .FirstOrDefaultAsync(b => b.Id == vm.BeneficiaryId && b.UserId == userId);

                    if (beneficiary == null)
                    {
                        ModelState.AddModelError(string.Empty, "Beneficiary not found.");
                        await PopulateDropdownsAsync();
                        return View(vm);
                    }

                    if (!string.IsNullOrEmpty(beneficiary.AccountSerialNumber))
                    {
                        result = await _transferService.SendToAccountAsync(
                            vm.SourceAccountId,
                            beneficiary.AccountSerialNumber,
                            vm.Amount,
                            userId,
                            vm.Note);
                    }
                    else
                    {
                        result = await _transferService.SendToMobileAsync(
                            vm.SourceAccountId,
                            beneficiary.MobileNumber!,
                            beneficiary.FullName,
                            vm.Amount,
                            userId,
                            vm.Note);
                    }
                    break;

                case TransferMode.DirectAccount:
                    result = await _transferService.SendToAccountAsync(
                        vm.SourceAccountId,
                        vm.DestinationSerial!,
                        vm.Amount,
                        userId,
                        vm.Note);
                    break;

                case TransferMode.DirectMobile:
                    result = await _transferService.SendToMobileAsync(
                        vm.SourceAccountId,
                        vm.RecipientMobile!,
                        vm.RecipientName!,
                        vm.Amount,
                        userId,
                        vm.Note);
                    break;

                default:
                    ModelState.AddModelError(string.Empty, "Invalid transfer mode.");
                    await PopulateDropdownsAsync();
                    return View(vm);
            }

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Transfer failed.");
                await PopulateDropdownsAsync();
                return View(vm);
            }

            TempData["Success"] = $"Transfer completed! Tracking: {result.Transfer!.TrackingNumber}";
            return RedirectToAction(nameof(Receipt), new { id = result.Transfer.Id });
        }

        // GET: /Transfer/Receipt/5
        public async Task<IActionResult> Receipt(int id)
        {
            var userId = _userManager.GetUserId(User);

            var transfer = await _db.Transfers
                .Include(t => t.SourceAccount).ThenInclude(a => a!.Currency)
                .Include(t => t.DestinationAccount).ThenInclude(a => a!.Currency)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null) return NotFound();

            // Security: only sender or recipient can see the receipt
            var isSender = transfer.SourceAccount?.UserId == userId;
            var isRecipient = transfer.DestinationAccount?.UserId == userId;

            if (!isSender && !isRecipient) return Forbid();

            ViewBag.CurrentUserId = userId;
            return View(transfer);
        }

        // GET: /Transfer/Track?tracking=TRX-...
        [AllowAnonymous]
        public async Task<IActionResult> Track(string? tracking)
        {
            if (string.IsNullOrWhiteSpace(tracking))
                return View((Transfer?)null);

            var transfer = await _db.Transfers
                .Include(t => t.SourceAccount).ThenInclude(a => a!.Currency)
                .Include(t => t.DestinationAccount).ThenInclude(a => a!.Currency)
                .FirstOrDefaultAsync(t => t.TrackingNumber == tracking.Trim());

            if (transfer == null)
            {
                ViewBag.NotFound = true;
                ViewBag.Tracking = tracking;
            }

            return View(transfer);
        }

        // --- helpers ---
        private async Task PopulateDropdownsAsync()
        {
            var userId = _userManager.GetUserId(User);

            var accounts = await _db.Accounts
                .Include(a => a.Currency)
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Currency.Code)
                .ToListAsync();

            ViewBag.Accounts = new SelectList(
                accounts.Select(a => new
                {
                    a.Id,
                    Label = $"{a.SerialNumber} — {a.Currency.Symbol}{a.Balance:N2} {a.Currency.Code}"
                }),
                "Id",
                "Label");

            var beneficiaries = await _db.Beneficiaries
                .Where(b => b.UserId == userId)
                .OrderBy(b => b.FullName)
                .ToListAsync();

            ViewBag.Beneficiaries = new SelectList(
                beneficiaries.Select(b => new
                {
                    b.Id,
                    Label = $"{b.FullName} — " +
                            (b.AccountSerialNumber ?? b.MobileNumber)
                }),
                "Id",
                "Label");
        }
    }
}