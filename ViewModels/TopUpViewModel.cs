using System.ComponentModel.DataAnnotations;

namespace KerzelPay.ViewModels
{
    public class TopUpViewModel
    {
        public int AccountId { get; set; }

        public string AccountSerialNumber { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public string CurrencySymbol { get; set; } = string.Empty;

        [Required]
        [Range(1, 100000, ErrorMessage = "Amount must be between 1 and 100,000.")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }
    }
}