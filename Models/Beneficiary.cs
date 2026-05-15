using System.ComponentModel.DataAnnotations;

namespace KerzelPay.Models
{
    public class Beneficiary
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string? AccountSerialNumber { get; set; }   // For account-to-account

        [MaxLength(20)]
        public string? MobileNumber { get; set; }          // For OMT-style transfers

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // FK -> Owner (ApplicationUser)
        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
    }
}