using System.ComponentModel.DataAnnotations;

namespace KerzelPay.ViewModels
{
    public class CreateAccountViewModel
    {
        [Required(ErrorMessage = "Please select a currency.")]
        [Display(Name = "Currency")]
        public int CurrencyId { get; set; }
    }
}