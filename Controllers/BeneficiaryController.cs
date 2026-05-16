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
    [Authorize(Roles = Roles.User)]
    public class BeneficiaryController : Controller
    {
        private readonly IRepository<Beneficiary> _beneficiaryRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public BeneficiaryController(
            IRepository<Beneficiary> beneficiaryRepo,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db)
        {
            _beneficiaryRepo = beneficiaryRepo;
            _userManager = userManager;
            _db = db;
        }

        // GET: /Beneficiary?q=anthony
        public async Task<IActionResult> Index(string? q)
        {
            var userId = _userManager.GetUserId(User);

            var query = _db.Beneficiaries
                .Where(b => b.UserId == userId);

            // Optional search
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(b =>
                    b.FullName.Contains(q) ||
                    (b.AccountSerialNumber != null && b.AccountSerialNumber.Contains(q)) ||
                    (b.MobileNumber != null && b.MobileNumber.Contains(q)));
            }

            var beneficiaries = await query
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            ViewBag.Query = q;
            return View(beneficiaries);
        }

        // GET: /Beneficiary/Create
        public IActionResult Create()
        {
            return View(new BeneficiaryViewModel());
        }

        // POST: /Beneficiary/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BeneficiaryViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var userId = _userManager.GetUserId(User)!;

            // Prevent adding yourself as beneficiary (if using account number)
            if (!string.IsNullOrWhiteSpace(vm.AccountSerialNumber))
            {
                var ownAccount = await _db.Accounts
                    .AnyAsync(a => a.SerialNumber == vm.AccountSerialNumber && a.UserId == userId);

                if (ownAccount)
                {
                    ModelState.AddModelError(nameof(vm.AccountSerialNumber),
                        "You cannot add your own account as a beneficiary.");
                    return View(vm);
                }
            }

            // Prevent duplicates
            var duplicate = await _db.Beneficiaries.AnyAsync(b =>
                b.UserId == userId &&
                ((vm.AccountSerialNumber != null && b.AccountSerialNumber == vm.AccountSerialNumber) ||
                 (vm.MobileNumber != null && b.MobileNumber == vm.MobileNumber)));

            if (duplicate)
            {
                ModelState.AddModelError(string.Empty,
                    "This beneficiary already exists in your list.");
                return View(vm);
            }

            var beneficiary = new Beneficiary
            {
                FullName = vm.FullName.Trim(),
                AccountSerialNumber = string.IsNullOrWhiteSpace(vm.AccountSerialNumber)
                    ? null
                    : vm.AccountSerialNumber.Trim().ToUpper(),
                MobileNumber = string.IsNullOrWhiteSpace(vm.MobileNumber)
                    ? null
                    : vm.MobileNumber.Trim(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _beneficiaryRepo.AddAsync(beneficiary);

            TempData["Success"] = $"{beneficiary.FullName} added to your beneficiaries.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Beneficiary/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);

            var beneficiary = await _db.Beneficiaries
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (beneficiary == null) return NotFound();

            var vm = new BeneficiaryViewModel
            {
                Id = beneficiary.Id,
                FullName = beneficiary.FullName,
                AccountSerialNumber = beneficiary.AccountSerialNumber,
                MobileNumber = beneficiary.MobileNumber
            };

            return View(vm);
        }

        // POST: /Beneficiary/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BeneficiaryViewModel vm)
        {
            if (id != vm.Id) return BadRequest();

            if (!ModelState.IsValid) return View(vm);

            var userId = _userManager.GetUserId(User);

            var beneficiary = await _db.Beneficiaries
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (beneficiary == null) return NotFound();

            beneficiary.FullName = vm.FullName.Trim();
            beneficiary.AccountSerialNumber = string.IsNullOrWhiteSpace(vm.AccountSerialNumber)
                ? null
                : vm.AccountSerialNumber.Trim().ToUpper();
            beneficiary.MobileNumber = string.IsNullOrWhiteSpace(vm.MobileNumber)
                ? null
                : vm.MobileNumber.Trim();

            await _beneficiaryRepo.UpdateAsync(beneficiary);

            TempData["Success"] = "Beneficiary updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Beneficiary/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);

            var beneficiary = await _db.Beneficiaries
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (beneficiary == null) return NotFound();

            return View(beneficiary);
        }

        // POST: /Beneficiary/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);

            var beneficiary = await _db.Beneficiaries
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (beneficiary == null) return NotFound();

            await _beneficiaryRepo.DeleteAsync(id);

            TempData["Success"] = "Beneficiary removed.";
            return RedirectToAction(nameof(Index));
        }
    }
}