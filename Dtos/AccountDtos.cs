using System.ComponentModel.DataAnnotations;

namespace KerzelPay.Dtos
{
    public class AccountDto
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string CurrencySymbol { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateAccountRequest
    {
        [Required]
        public int CurrencyId { get; set; }
    }

    public class CurrencyDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal ExchangeRateToUsd { get; set; }
    }
}