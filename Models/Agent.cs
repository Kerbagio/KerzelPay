using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KerzelPay.Models
{
    public enum AgentStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Suspended = 4
    }

    public class Agent
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string StoreName { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        [Column(TypeName = "decimal(9,6)")]
        public decimal Latitude { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal Longitude { get; set; }

        [MaxLength(100)]
        public string? WorkingHours { get; set; }   // e.g. "Mon-Fri 9:00-18:00"

        public AgentStatus Status { get; set; } = AgentStatus.Pending;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCommission { get; set; } = 0m;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Link to ApplicationUser (agents log in too)
        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public ICollection<Transfer> Transfers { get; set; } = new List<Transfer>();

        public bool IsResubmission { get; set; } = false;
    }
}