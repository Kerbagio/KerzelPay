using System.ComponentModel.DataAnnotations;

namespace KerzelPay.ViewModels
{
    public class BeneficiaryViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter the recipient's full name.")]
        [Display(Name = "Full name")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Kerzel Pay account number")]
        [StringLength(30)]
        [RegularExpression(@"^KP-\d{4}-[A-Z0-9]{6}$",
            ErrorMessage = "Account number must look like KP-2026-XXXXXX.")]
        public string? AccountSerialNumber { get; set; }

        [Display(Name = "Mobile number")]
        [StringLength(20)]
        [RegularExpression(@"^\+?[0-9\s\-]{6,20}$",
            ErrorMessage = "Please enter a valid mobile number.")]
        public string? MobileNumber { get; set; }

        // Custom validation: at least one of AccountSerialNumber OR MobileNumber must be filled
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(AccountSerialNumber) &&
                string.IsNullOrWhiteSpace(MobileNumber))
            {
                yield return new ValidationResult(
                    "Provide either an account number or a mobile number.",
                    new[] { nameof(AccountSerialNumber), nameof(MobileNumber) });
            }
        }
    }
}