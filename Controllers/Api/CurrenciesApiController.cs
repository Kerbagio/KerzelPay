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
    }
}