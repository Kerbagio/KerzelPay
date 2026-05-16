using System.ComponentModel.DataAnnotations;

namespace KerzelPay.ViewModels
{
    public class ReviewViewModel
    {
        [Required]
        [Range(1, 5, ErrorMessage = "Please pick a rating between 1 and 5 stars.")]
        public int Rating { get; set; }

        [StringLength(500, ErrorMessage = "Comment can't exceed 500 characters.")]
        public string? Comment { get; set; }
    }
}