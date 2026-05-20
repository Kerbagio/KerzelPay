using System.ComponentModel.DataAnnotations;

namespace KerzelPay.ViewModels
{
    public class CashInViewModel
    {
        [Required(ErrorMessage = "Customer's account number is required.")]
        [Display(Name = "Customer account number")]
        [RegularExpression(@"^KP-\d{4}-[A-Z0-9]{6}$",
            ErrorMessage = "Account number must look like KP-2026-XXXXXX.")]
        public string CustomerAccountSerial { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, 100000, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [StringLength(250)]
        [Display(Name = "Note (optional)")]
        public string? Note { get; set; }
    }
}