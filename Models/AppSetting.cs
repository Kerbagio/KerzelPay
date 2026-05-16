using System.ComponentModel.DataAnnotations;

namespace KerzelPay.Models
{
    public class AppSetting
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Key { get; set; } = string.Empty;   // e.g. "CommissionPercent"

        [Required, MaxLength(500)]
        public string Value { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}