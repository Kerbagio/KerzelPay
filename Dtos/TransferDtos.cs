using System.ComponentModel.DataAnnotations;

namespace KerzelPay.Dtos
{
    public class TransferDto
    {
        public int Id { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? SourceSerial { get; set; }
        public string? DestinationSerial { get; set; }
        public string? RecipientMobile { get; set; }
        public string? RecipientName { get; set; }
        public decimal Amount { get; set; }
        public decimal ConvertedAmount { get; set; }
        public decimal ExchangeRate { get; set; }
        public decimal Commission { get; set; }
        public string SourceCurrencyCode { get; set; } = string.Empty;
        public string DestinationCurrencyCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Note { get; set; }
    }

    public class CreateTransferRequest
    {
        [Required]
        public int SourceAccountId { get; set; }

        [Required]
        [RegularExpression("^(Account|Mobile)$",
            ErrorMessage = "Mode must be 'Account' or 'Mobile'.")]
        public string Mode { get; set; } = "Account";

        public string? DestinationSerial { get; set; }
        public string? RecipientMobile { get; set; }
        public string? RecipientName { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public decimal Amount { get; set; }

        public string? Note { get; set; }
    }
}