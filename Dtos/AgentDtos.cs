namespace KerzelPay.Dtos
{
    public class AgentDto
    {
        public int Id { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? WorkingHours { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}