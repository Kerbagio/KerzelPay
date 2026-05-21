using System.ComponentModel.DataAnnotations;

namespace KerzelPay.ViewModels
{
    public class AgentApplicationViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "Store name is required.")]
        [Display(Name = "Store name")]
        [StringLength(100)]
        public string StoreName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        // -- Working hours, structured --
        [Required(ErrorMessage = "Please select working days.")]
        [Display(Name = "Working days")]
        public string WorkingDays { get; set; } = "Mon-Fri";

        [Required(ErrorMessage = "Please pick an opening time.")]
        [Display(Name = "Open at")]
        [DataType(DataType.Time)]
        public TimeOnly OpenTime { get; set; } = new TimeOnly(9, 0);

        [Required(ErrorMessage = "Please pick a closing time.")]
        [Display(Name = "Close at")]
        [DataType(DataType.Time)]
        public TimeOnly CloseTime { get; set; } = new TimeOnly(18, 0);

        // -- Location (set by the map click) --
        [Range(-90, 90)]
        public decimal Latitude { get; set; }

        [Range(-180, 180)]
        public decimal Longitude { get; set; }

        // Helper: format hours into one string for the DB
        public string FormatWorkingHours() =>
            $"{WorkingDays} {OpenTime:HH:mm}-{CloseTime:HH:mm}";

        // Custom validation
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Must have a location pinned (lat & lng cannot both be 0)
            if (Latitude == 0 && Longitude == 0)
            {
                yield return new ValidationResult(
                    "Please click on the map to pick your store's location.",
                    new[] { nameof(Latitude) });
            }

            // Close time must be after open time
            if (CloseTime <= OpenTime)
            {
                yield return new ValidationResult(
                    "Closing time must be later than opening time.",
                    new[] { nameof(CloseTime) });
            }
        }

        public int Id { get; set; }   // 0 for new application, > 0 for edit

        public static (string Days, TimeOnly Open, TimeOnly Close) ParseWorkingHours(string? hours)
        {
            if (string.IsNullOrWhiteSpace(hours))
                return ("Mon-Fri", new TimeOnly(9, 0), new TimeOnly(18, 0));

            // Format: "Mon-Sat 9:00-18:00"
            var parts = hours.Trim().Split(' ', 2);
            var days = parts.Length > 0 ? parts[0] : "Mon-Fri";

            var open = new TimeOnly(9, 0);
            var close = new TimeOnly(18, 0);

            if (parts.Length == 2)
            {
                var timeParts = parts[1].Split('-');
                if (timeParts.Length == 2)
                {
                    TimeOnly.TryParse(timeParts[0], out open);
                    TimeOnly.TryParse(timeParts[1], out close);
                }
            }

            return (days, open, close);
        }
    }
}