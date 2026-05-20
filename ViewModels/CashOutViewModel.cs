using System.ComponentModel.DataAnnotations;

namespace KerzelPay.ViewModels
{
    public class CashOutViewModel
    {
        [Required(ErrorMessage = "Tracking number is required.")]
        [Display(Name = "Transfer tracking number")]
        public string TrackingNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please record the recipient's ID proof.")]
        [Display(Name = "Recipient ID proof")]
        [StringLength(50)]
        public string RecipientIdProof { get; set; } = string.Empty;

        // For confirmation display before final submit
        public bool Confirmed { get; set; } = false;
    }
}