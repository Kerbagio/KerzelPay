using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Dtos;
using KerzelPay.Models;
using KerzelPay.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Controllers.Api
{
    [ApiController]
    [Route("api/transfers")]
    [Produces("application/json")]
    public class TransfersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TransferService _transferService;

        public TransfersApiController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            TransferService transferService)
        {
            _db = db;
            _userManager = userManager;
            _transferService = transferService;
        }

        /// <summary>Public — track a transfer by its tracking number.</summary>
        [HttpGet("track/{trackingNumber}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(TransferDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Track(string trackingNumber)
        {
            var t = await _db.Transfers
                .Include(t => t.SourceAccount)
                .Include(t => t.DestinationAccount)
                .FirstOrDefaultAsync(t => t.TrackingNumber == trackingNumber);

            if (t == null) return NotFound();
            return Ok(MapToDto(t));
        }

        /// <summary>List my transfers (sent or received).</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.User)]
        [ProducesResponseType(typeof(IEnumerable<TransferDto>), 200)]
        public async Task<IActionResult> GetMyTransfers()
        {
            var userId = _userManager.GetUserId(User);

            var transfers = await _db.Transfers
                .Include(t => t.SourceAccount)
                .Include(t => t.DestinationAccount)
                .Where(t =>
                    (t.SourceAccount != null && t.SourceAccount.UserId == userId) ||
                    (t.DestinationAccount != null && t.DestinationAccount.UserId == userId))
                .OrderByDescending(t => t.CreatedAt)
                .Take(100)
                .ToListAsync();

            return Ok(transfers.Select(MapToDto));
        }

        /// <summary>Initiate a new transfer.</summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.User)]
        [ProducesResponseType(typeof(TransferDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateTransfer([FromBody] CreateTransferRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = _userManager.GetUserId(User)!;
            TransferResult result;

            if (req.Mode == "Account")
            {
                if (string.IsNullOrWhiteSpace(req.DestinationSerial))
                    return BadRequest(new { error = "DestinationSerial is required for Account mode." });

                result = await _transferService.SendToAccountAsync(
                    req.SourceAccountId,
                    req.DestinationSerial,
                    req.Amount,
                    userId,
                    req.Note);
            }
            else // Mobile
            {
                if (string.IsNullOrWhiteSpace(req.RecipientMobile) || string.IsNullOrWhiteSpace(req.RecipientName))
                    return BadRequest(new { error = "RecipientMobile and RecipientName are required for Mobile mode." });

                result = await _transferService.SendToMobileAsync(
                    req.SourceAccountId,
                    req.RecipientMobile,
                    req.RecipientName,
                    req.Amount,
                    userId,
                    req.Note);
            }

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage });

            return StatusCode(201, MapToDto(result.Transfer!));
        }

        // ---- helper ----
        private static TransferDto MapToDto(Transfer t) => new()
        {
            Id = t.Id,
            TrackingNumber = t.TrackingNumber,
            Type = t.Type.ToString(),
            Status = t.Status.ToString(),
            SourceSerial = t.SourceAccount?.SerialNumber,
            DestinationSerial = t.DestinationAccount?.SerialNumber,
            RecipientMobile = t.RecipientMobile,
            RecipientName = t.RecipientName,
            Amount = t.Amount,
            ConvertedAmount = t.ConvertedAmount,
            ExchangeRate = t.ExchangeRate,
            Commission = t.Commission,
            SourceCurrencyCode = t.SourceCurrencyCode,
            DestinationCurrencyCode = t.DestinationCurrencyCode,
            CreatedAt = t.CreatedAt,
            CompletedAt = t.CompletedAt,
            Note = t.Note
        };
    }
}