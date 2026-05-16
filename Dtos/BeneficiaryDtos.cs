using System.ComponentModel.DataAnnotations;

namespace KerzelPay.Dtos
{
    public class BeneficiaryDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AccountSerialNumber { get; set; }
        public string? MobileNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateBeneficiaryRequest
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        public string? AccountSerialNumber { get; set; }
        public string? MobileNumber { get; set; }
    }
}