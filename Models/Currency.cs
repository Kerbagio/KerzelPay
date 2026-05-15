using System.ComponentModel.DataAnnotations;

namespace KerzelPay.Models
{
    public class Currency
    {
        public int Id { get; set; }

        [Required, MaxLength(3)]
        public string Code { get; set; } = string.Empty;   // USD, EUR, LBP

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;   // US Dollar, Euro

        [MaxLength(5)]
        public string Symbol { get; set; } = string.Empty; // $, €, ل.ل

        // Exchange rate relative to USD (1 USD = ExchangeRateToUsd of this currency)
        public decimal ExchangeRateToUsd { get; set; } = 1m;

        public bool IsActive { get; set; } = true;

        public ICollection<Account> Accounts { get; set; } = new List<Account>();
    }
}