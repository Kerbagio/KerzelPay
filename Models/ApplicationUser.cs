using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace KerzelPay.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<Account> Accounts { get; set; } = new List<Account>();
        public ICollection<Beneficiary> Beneficiaries { get; set; } = new List<Beneficiary>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}