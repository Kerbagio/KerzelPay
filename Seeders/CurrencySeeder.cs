using KerzelPay.Data;
using KerzelPay.Models;

namespace KerzelPay.Seeders
{
    public static class CurrencySeeder
    {
        public static void SeedCurrencies(ApplicationDbContext context)
        {
            if (context.Currencies.Any()) return;

            context.Currencies.AddRange(
                new Currency { Code = "USD", Name = "US Dollar", Symbol = "$", ExchangeRateToUsd = 1m, IsActive = true },
                new Currency { Code = "EUR", Name = "Euro", Symbol = "€", ExchangeRateToUsd = 0.92m, IsActive = true },
                new Currency { Code = "GBP", Name = "British Pound", Symbol = "£", ExchangeRateToUsd = 0.79m, IsActive = true },
                new Currency { Code = "LBP", Name = "Lebanese Pound", Symbol = "ل.ل", ExchangeRateToUsd = 89500m, IsActive = true }
            );

            context.SaveChanges();
        }
    }
}