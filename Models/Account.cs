using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KerzelPay.Models
{
    public class Account
    {
        public int Id { get; set; }

        // Unique account serial number (e.g. KP-2026-0001)
        [Required, MaxLength(30)]
        public string SerialNumber { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0m;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // FK -> Currency
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; } = null!;

        // FK -> ApplicationUser
        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
    }
}