using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KerzelPay.Models
{
    public enum TransferType
    {
        AccountToAccount = 1,
        MobileOmt = 2,
        TopUp = 3,         // Stripe top-up
        CashOut = 4        // Agent cash-out
    }

    public enum TransferStatus
    {
        Pending = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
    }

    public class Transfer
    {
        public int Id { get; set; }

        // Unique tracking number (e.g. TRX-20260315-ABC123)
        [Required, MaxLength(40)]
        public string TrackingNumber { get; set; } = string.Empty;

        public TransferType Type { get; set; }
        public TransferStatus Status { get; set; } = TransferStatus.Pending;

        // Sender (the user initiating)
        public int? SourceAccountId { get; set; }
        public Account? SourceAccount { get; set; }

        // Destination — either an Account (A2A) OR Mobile/Name (OMT)
        public int? DestinationAccountId { get; set; }
        public Account? DestinationAccount { get; set; }

        [MaxLength(20)]
        public string? RecipientMobile { get; set; }

        [MaxLength(100)]
        public string? RecipientName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }              // In source currency

        [Column(TypeName = "decimal(18,2)")]
        public decimal ConvertedAmount { get; set; }     // In destination currency

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRate { get; set; } = 1m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Commission { get; set; } = 0m;

        [MaxLength(3)]
        public string SourceCurrencyCode { get; set; } = string.Empty;

        [MaxLength(3)]
        public string DestinationCurrencyCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        [MaxLength(250)]
        public string? Note { get; set; }

        // If processed by an agent (cash-in/out)
        public int? AgentId { get; set; }
        public Agent? Agent { get; set; }
    }
}