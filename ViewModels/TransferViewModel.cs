using System.ComponentModel.DataAnnotations;

namespace KerzelPay.ViewModels
{
    public enum TransferMode
    {
        Beneficiary = 1,   // Pick from saved beneficiary list
        DirectAccount = 2, // Type a Kerzel Pay account number directly
        DirectMobile = 3   // Type a mobile number directly (OMT-style)
    }

    public class TransferViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "Please select a source account.")]
        [Display(Name = "Send from")]
        public int SourceAccountId { get; set; }

        [Required]
        public TransferMode Mode { get; set; } = TransferMode.Beneficiary;

        // -- Mode 1: pick a beneficiary --
        [Display(Name = "Beneficiary")]
        public int? BeneficiaryId { get; set; }

        // -- Mode 2: direct account number --
        [Display(Name = "Destination account number")]
        [RegularExpression(@"^KP-\d{4}-[A-Z0-9]{6}$",
            ErrorMessage = "Account number must look like KP-2026-XXXXXX.")]
        public string? DestinationSerial { get; set; }

        // -- Mode 3: direct mobile --
        [Display(Name = "Recipient mobile")]
        [RegularExpression(@"^\+?[0-9\s\-]{6,20}$",
            ErrorMessage = "Please enter a valid mobile number.")]
        public string? RecipientMobile { get; set; }

        [Display(Name = "Recipient name")]
        [StringLength(100)]
        public string? RecipientName { get; set; }

        // -- Common --
        [Required(ErrorMessage = "Please enter an amount.")]
        [Range(0.01, 1000000, ErrorMessage = "Amount must be greater than 0.")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [StringLength(250)]
        public string? Note { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            switch (Mode)
            {
                case TransferMode.Beneficiary:
                    if (BeneficiaryId == null || BeneficiaryId <= 0)
                        yield return new ValidationResult(
                            "Please select a beneficiary.",
                            new[] { nameof(BeneficiaryId) });
                    break;

                case TransferMode.DirectAccount:
                    if (string.IsNullOrWhiteSpace(DestinationSerial))
                        yield return new ValidationResult(
                            "Please enter the destination account number.",
                            new[] { nameof(DestinationSerial) });
                    break;

                case TransferMode.DirectMobile:
                    if (string.IsNullOrWhiteSpace(RecipientMobile))
                        yield return new ValidationResult(
                            "Please enter the recipient's mobile number.",
                            new[] { nameof(RecipientMobile) });
                    if (string.IsNullOrWhiteSpace(RecipientName))
                        yield return new ValidationResult(
                            "Please enter the recipient's name.",
                            new[] { nameof(RecipientName) });
                    break;
            }
        }
    }
}