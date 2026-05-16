using KerzelPay.Constants;
using KerzelPay.Helpers;
using KerzelPay.Models;
using KerzelPay.Repositories;
using KerzelPay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using KerzelPay.Data;

namespace KerzelPay.Controllers
{
    [Authorize(Roles = Roles.User)]
    public class AccountController : Controller
    {
        private readonly IRepository<Account> _accountRepo;
        private readonly IRepository<Currency> _currencyRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public AccountController(
            IRepository<Account> accountRepo,
            IRepository<Currency> currencyRepo,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db)
        {
            _accountRepo = accountRepo;
            _currencyRepo = currencyRepo;
            _userManager = userManager;
            _db = db;
        }

        // GET: /Account
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Get only this user's accounts, with Currency eagerly loaded
            var accounts = await _db.Accounts
                .Include(a => a.Currency)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(accounts);
        }

        // GET: /Account/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCurrenciesAsync();
            return View(new CreateAccountViewModel());
        }

        // POST: /Account/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAccountViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCurrenciesAsync();
                return View(vm);
            }

            // Validate that the currency exists & is active
            var currency = await _currencyRepo.GetByIdAsync(vm.CurrencyId);
            if (currency == null || !currency.IsActive)
            {
                ModelState.AddModelError(nameof(vm.CurrencyId), "Selected currency is not available.");
                await PopulateCurrenciesAsync();
                return View(vm);
            }

            var userId = _userManager.GetUserId(User)!;

            var account = new Account
            {
                SerialNumber = SerialNumberGenerator.GenerateAccountSerial(),
                Balance = 0m,
                CurrencyId = vm.CurrencyId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _accountRepo.AddAsync(account);

            TempData["Success"] = $"Account {account.SerialNumber} created successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Account/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);

            var account = await _db.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null) return NotFound();

            return View(account);
        }

        // GET: /Account/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);

            var account = await _db.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null) return NotFound();

            return View(account);
        }

        // POST: /Account/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);

            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null) return NotFound();

            // Safety: don't allow deleting an account that still has money in it
            if (account.Balance > 0)
            {
                TempData["Error"] = "Cannot delete an account with a positive balance. Transfer the funds out first.";
                return RedirectToAction(nameof(Index));
            }

            await _accountRepo.DeleteAsync(id);

            TempData["Success"] = "Account deleted.";
            return RedirectToAction(nameof(Index));
        }

        // --- helper ---
        private async Task PopulateCurrenciesAsync()
        {
            var currencies = await _currencyRepo.GetAllAsync();
            ViewBag.Currencies = new SelectList(
                currencies.Where(c => c.IsActive),
                nameof(Currency.Id),
                nameof(Currency.Code));
        }
    }
}