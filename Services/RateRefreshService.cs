using KerzelPay.Data;
using System.Text.Json;

namespace KerzelPay.Services
{
    public class RateRefreshService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RateRefreshService> _logger;

        // Currencies we want live rates for (LBP excluded on purpose — admin manages it)
        private static readonly string[] LiveCurrencyCodes = { "EUR", "GBP" };

        public RateRefreshService(
            ApplicationDbContext db,
            IHttpClientFactory httpClientFactory,
            ILogger<RateRefreshService> logger)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Fetches today's rates from Frankfurter (ECB) and updates the DB.
        /// Returns true on success, false on any failure (app keeps working with cached rates).
        /// </summary>
        public async Task<RefreshResult> RefreshRatesAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("Frankfurter");

                // Ask Frankfurter: "1 USD = how much EUR, GBP?"
                var symbols = string.Join(",", LiveCurrencyCodes);
                var response = await client.GetAsync($"latest?from=USD&to={symbols}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Frankfurter API returned {StatusCode}. Keeping cached rates.",
                        response.StatusCode);
                    return RefreshResult.Failed($"API returned {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("rates", out var ratesElement))
                {
                    _logger.LogWarning("Frankfurter response missing 'rates'. Keeping cached rates.");
                    return RefreshResult.Failed("Invalid API response");
                }

                // Build a code → rate dictionary from the API response
                var liveRates = new Dictionary<string, decimal>();
                foreach (var rate in ratesElement.EnumerateObject())
                {
                    liveRates[rate.Name] = rate.Value.GetDecimal();
                }
                liveRates["USD"] = 1m; // USD is always 1 (it's our base)

                // Update the DB (only for our live currencies)
                var currenciesToUpdate = _db.Currencies
                    .Where(c => LiveCurrencyCodes.Contains(c.Code) && c.IsActive)
                    .ToList();

                int updatedCount = 0;
                var updatedRates = new Dictionary<string, decimal>();

                foreach (var currency in currenciesToUpdate)
                {
                    if (liveRates.TryGetValue(currency.Code, out var newRate))
                    {
                        currency.ExchangeRateToUsd = Math.Round(newRate, 6);
                        updatedRates[currency.Code] = currency.ExchangeRateToUsd;
                        updatedCount++;
                        _logger.LogInformation(
                            "Updated {Code}: 1 USD = {Rate}",
                            currency.Code, currency.ExchangeRateToUsd);
                    }
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Currency rates refreshed from Frankfurter ({Count} updated)", updatedCount);

                return RefreshResult.Success(updatedCount, updatedRates);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    "Network error reaching Frankfurter: {Message}. Keeping cached rates.",
                    ex.Message);
                return RefreshResult.Failed("Network unavailable");
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Frankfurter request timed out. Keeping cached rates.");
                return RefreshResult.Failed("API timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error refreshing currency rates");
                return RefreshResult.Failed(ex.Message);
            }
        }
    }

    public class RefreshResult
    {
        public bool IsSuccess { get; set; }
        public int UpdatedCount { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, decimal> UpdatedRates { get; set; } = new();
        public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;

        public static RefreshResult Success(int count, Dictionary<string, decimal> rates) =>
            new() { IsSuccess = true, UpdatedCount = count, UpdatedRates = rates };

        public static RefreshResult Failed(string error) =>
            new() { IsSuccess = false, ErrorMessage = error };
    }
}