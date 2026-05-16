using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Dtos;
using KerzelPay.Helpers;
using KerzelPay.Models;
using KerzelPay.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Controllers.Api
{
    [ApiController]
    [Route("api/accounts")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.User)]
    public class AccountsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRepository<Account> _accountRepo;
        private readonly IRepository<Currency> _currencyRepo;

        public AccountsApiController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IRepository<Account> accountRepo,
            IRepository<Currency> currencyRepo)
        {
            _db = db;
            _userManager = userManager;
            _accountRepo = accountRepo;
            _currencyRepo = currencyRepo;
        }

        /// <summary>List my accounts.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<AccountDto>), 200)]
        public async Task<IActionResult> GetMyAccounts()
        {
            var userId = _userManager.GetUserId(User);

            var accounts = await _db.Accounts
                .Include(a => a.Currency)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AccountDto
                {
                    Id = a.Id,
                    SerialNumber = a.SerialNumber,
                    Balance = a.Balance,
                    CurrencyCode = a.Currency.Code,
                    CurrencySymbol = a.Currency.Symbol,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            return Ok(accounts);
        }

        /// <summary>Get a single account by ID.</summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(AccountDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetAccount(int id)
        {
            var userId = _userManager.GetUserId(User);

            var account = await _db.Accounts
                .Include(a => a.Currency)
                .Where(a => a.Id == id && a.UserId == userId)
                .Select(a => new AccountDto
                {
                    Id = a.Id,
                    SerialNumber = a.SerialNumber,
                    Balance = a.Balance,
                    CurrencyCode = a.Currency.Code,
                    CurrencySymbol = a.Currency.Symbol,
                    CreatedAt = a.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (account == null) return NotFound();
            return Ok(account);
        }

        /// <summary>Create a new account in the specified currency.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(AccountDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currency = await _currencyRepo.GetByIdAsync(req.CurrencyId);
            if (currency == null || !currency.IsActive)
                return BadRequest(new { error = "Currency not available." });

            var userId = _userManager.GetUserId(User)!;

            var account = new Account
            {
                SerialNumber = SerialNumberGenerator.GenerateAccountSerial(),
                Balance = 0m,
                CurrencyId = req.CurrencyId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _accountRepo.AddAsync(account);

            return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, new AccountDto
            {
                Id = account.Id,
                SerialNumber = account.SerialNumber,
                Balance = 0m,
                CurrencyCode = currency.Code,
                CurrencySymbol = currency.Symbol,
                CreatedAt = account.CreatedAt
            });
        }
    }
}