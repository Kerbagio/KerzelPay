namespace KerzelPay.Helpers
{
    public static class SerialNumberGenerator
    {
        // Format: KP-YYYY-XXXXXX (e.g. KP-2026-A4F2C8)
        public static string GenerateAccountSerial()
        {
            var year = DateTime.UtcNow.Year;
            var randomPart = Guid.NewGuid().ToString("N")[..6].ToUpper();
            return $"KP-{year}-{randomPart}";
        }

        // Format: TRX-YYYYMMDD-XXXXXX (e.g. TRX-20260315-B2D9F1)
        public static string GenerateTransferTrackingNumber()
        {
            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            var randomPart = Guid.NewGuid().ToString("N")[..6].ToUpper();
            return $"TRX-{date}-{randomPart}";
        }
    }
}