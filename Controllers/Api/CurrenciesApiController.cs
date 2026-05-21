using KerzelPay.Data;
using KerzelPay.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Controllers.Api
{
    [ApiController]
    [Route("api/currencies")]
    [Produces("application/json")]
    public class CurrenciesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public CurrenciesApiController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>Public — list active currencies and their rates.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CurrencyDto>), 200)]
        public async Task<IActionResult> GetActive()
        {
            var currencies = await _db.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .Select(c => new CurrencyDto
                {
                    Id = c.Id,
                    Code = c.Code,
                    Name = c.Name,
                    Symbol = c.Symbol,
                    ExchangeRateToUsd = c.ExchangeRateToUsd
                })
                .ToListAsync();

            return Ok(currencies);
        }

        /// <summary>Public — calculate a conversion + commission for a hypothetical transfer.</summary>
        [HttpGet("convert")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Convert(
            [FromQuery] string from,
            [FromQuery] string to,
            [FromQuery] decimal amount,
            [FromServices] Services.CurrencyService currencyService)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return BadRequest(new { error = "Both 'from' and 'to' are required." });

            if (amount <= 0)
                return BadRequest(new { error = "Amount must be greater than 0." });

            try
            {
                // Get conversion result
                var conversion = await currencyService.ConvertAsync(amount, from, to);

                // Get the current commission rate from AppSettings
                var commissionSetting = await _db.AppSettings
                    .FirstOrDefaultAsync(s => s.Key == "CommissionPercent");

                decimal commissionPercent = 1.0m;
                if (commissionSetting != null && decimal.TryParse(commissionSetting.Value, out var rate))
                    commissionPercent = rate;

                var commission = Math.Round(amount * (commissionPercent / 100m), 2);
                var totalDebit = amount + commission;

                return Ok(new
                {
                    originalAmount = conversion.OriginalAmount,
                    convertedAmount = conversion.ConvertedAmount,
                    exchangeRate = conversion.ExchangeRate,
                    fromCurrency = conversion.FromCurrency,
                    toCurrency = conversion.ToCurrency,
                    commissionPercent = commissionPercent,
                    commission = commission,
                    totalDebit = totalDebit
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}