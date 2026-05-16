using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Dtos;
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
    [Route("api/beneficiaries")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.User)]
    public class BeneficiariesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRepository<Beneficiary> _beneficiaryRepo;

        public BeneficiariesApiController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IRepository<Beneficiary> beneficiaryRepo)
        {
            _db = db;
            _userManager = userManager;
            _beneficiaryRepo = beneficiaryRepo;
        }

        [HttpGet]
        public async Task<IActionResult> GetMine()
        {
            var userId = _userManager.GetUserId(User);

            var list = await _db.Beneficiaries
                .Where(b => b.UserId == userId)
                .OrderBy(b => b.FullName)
                .Select(b => new BeneficiaryDto
                {
                    Id = b.Id,
                    FullName = b.FullName,
                    AccountSerialNumber = b.AccountSerialNumber,
                    MobileNumber = b.MobileNumber,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBeneficiaryRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(req.AccountSerialNumber) &&
                string.IsNullOrWhiteSpace(req.MobileNumber))
            {
                return BadRequest(new { error = "Provide either an account number or a mobile number." });
            }

            var userId = _userManager.GetUserId(User)!;

            var beneficiary = new Beneficiary
            {
                FullName = req.FullName.Trim(),
                AccountSerialNumber = string.IsNullOrWhiteSpace(req.AccountSerialNumber)
                    ? null : req.AccountSerialNumber.Trim().ToUpper(),
                MobileNumber = string.IsNullOrWhiteSpace(req.MobileNumber)
                    ? null : req.MobileNumber.Trim(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _beneficiaryRepo.AddAsync(beneficiary);

            return StatusCode(201, new BeneficiaryDto
            {
                Id = beneficiary.Id,
                FullName = beneficiary.FullName,
                AccountSerialNumber = beneficiary.AccountSerialNumber,
                MobileNumber = beneficiary.MobileNumber,
                CreatedAt = beneficiary.CreatedAt
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);

            var beneficiary = await _db.Beneficiaries
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (beneficiary == null) return NotFound();

            await _beneficiaryRepo.DeleteAsync(id);
            return NoContent();
        }
    }
}