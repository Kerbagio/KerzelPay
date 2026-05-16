using System.ComponentModel.DataAnnotations;

namespace KerzelPay.ViewModels
{
    public class AgentApplicationViewModel
    {
        [Required(ErrorMessage = "Store name is required.")]
        [Display(Name = "Store name")]
        [StringLength(100)]
        public string StoreName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please pick a location on the map.")]
        [Range(-90, 90)]
        public decimal Latitude { get; set; }

        [Required(ErrorMessage = "Please pick a location on the map.")]
        [Range(-180, 180)]
        public decimal Longitude { get; set; }

        [Display(Name = "Working hours")]
        [StringLength(100)]
        public string? WorkingHours { get; set; }
    }
}