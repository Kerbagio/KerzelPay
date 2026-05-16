using KerzelPay.Models;
using KerzelPay.Repositories;

namespace KerzelPay.Services
{
    public class CurrencyService
    {
        private readonly IRepository<Currency> _currencyRepo;

        public CurrencyService(IRepository<Currency> currencyRepo)
        {
            _currencyRepo = currencyRepo;
        }

        /// <summary>
        /// Converts an amount from one currency to another.
        /// Logic: amount_in_target = amount_in_source / rateSource * rateTarget
        /// where rate is "how many of this currency = 1 USD".
        /// </summary>
        public async Task<ConversionResult> ConvertAsync(
            decimal amount,
            string fromCurrencyCode,
            string toCurrencyCode)
        {
            // Same currency: no conversion needed
            if (string.Equals(fromCurrencyCode, toCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return new ConversionResult
                {
                    OriginalAmount = amount,
                    ConvertedAmount = amount,
                    ExchangeRate = 1m,
                    FromCurrency = fromCurrencyCode,
                    ToCurrency = toCurrencyCode
                };
            }

            var currencies = await _currencyRepo.GetAllAsync();

            var from = currencies.FirstOrDefault(c =>
                c.Code.Equals(fromCurrencyCode, StringComparison.OrdinalIgnoreCase));

            var to = currencies.FirstOrDefault(c =>
                c.Code.Equals(toCurrencyCode, StringComparison.OrdinalIgnoreCase));

            if (from == null || to == null)
                throw new InvalidOperationException(
                    $"Currency not found: {fromCurrencyCode} or {toCurrencyCode}");

            // Bridge through USD: amount → USD → target
            var amountInUsd = amount / from.ExchangeRateToUsd;
            var converted = amountInUsd * to.ExchangeRateToUsd;

            // The effective rate the user will see (1 unit of source = X of target)
            var effectiveRate = to.ExchangeRateToUsd / from.ExchangeRateToUsd;

            return new ConversionResult
            {
                OriginalAmount = amount,
                ConvertedAmount = Math.Round(converted, 2),
                ExchangeRate = Math.Round(effectiveRate, 6),
                FromCurrency = from.Code,
                ToCurrency = to.Code
            };
        }
    }

    public class ConversionResult
    {
        public decimal OriginalAmount { get; set; }
        public decimal ConvertedAmount { get; set; }
        public decimal ExchangeRate { get; set; }
        public string FromCurrency { get; set; } = string.Empty;
        public string ToCurrency { get; set; } = string.Empty;
    }
}