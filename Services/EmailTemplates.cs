using KerzelPay.Models;

namespace KerzelPay.Services
{
    public static class EmailTemplates
    {
        private const string BrandColor = "#0d6efd";
        private const string AccentColor = "#198754";

        private static string Wrapper(string title, string contentHtml) => $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>{title}</title></head>
<body style='margin:0; padding:0; font-family: -apple-system, BlinkMacSystemFont, sans-serif; background:#f4f4f7;'>
    <div style='max-width:600px; margin:30px auto; background:#fff; border-radius:8px; overflow:hidden; box-shadow:0 2px 4px rgba(0,0,0,0.05);'>
        <div style='background:{BrandColor}; color:#fff; padding:24px; text-align:center;'>
            <h1 style='margin:0; font-size:24px;'>💸 Kerzel Pay</h1>
        </div>
        <div style='padding:32px;'>
            {contentHtml}
        </div>
        <div style='background:#f8f9fa; padding:16px; text-align:center; color:#6c757d; font-size:12px;'>
            This is an automated message from Kerzel Pay.<br/>
            Do not reply directly to this email.
        </div>
    </div>
</body>
</html>";

        public static string TransferSent(Transfer transfer, string recipientLabel)
        {
            var body = $@"
                <h2 style='color:{BrandColor}; margin-top:0;'>Transfer sent ✅</h2>
                <p>Hi,</p>
                <p>Your transfer has been completed successfully.</p>

                <table style='width:100%; border-collapse:collapse; margin:20px 0;'>
                    <tr><td style='padding:8px 0; color:#6c757d;'>Amount</td>
                        <td style='padding:8px 0; text-align:right;'><strong>{transfer.SourceCurrencyCode} {transfer.Amount:N2}</strong></td></tr>
                    <tr><td style='padding:8px 0; color:#6c757d;'>To</td>
                        <td style='padding:8px 0; text-align:right;'>{recipientLabel}</td></tr>
                    <tr><td style='padding:8px 0; color:#6c757d;'>Commission</td>
                        <td style='padding:8px 0; text-align:right;'>{transfer.SourceCurrencyCode} {transfer.Commission:N2}</td></tr>
                    <tr><td style='padding:8px 0; color:#6c757d;'>Tracking number</td>
                        <td style='padding:8px 0; text-align:right;'><code>{transfer.TrackingNumber}</code></td></tr>
                    <tr><td style='padding:8px 0; color:#6c757d;'>Date</td>
                        <td style='padding:8px 0; text-align:right;'>{transfer.CompletedAt:dd MMM yyyy HH:mm}</td></tr>
                </table>

                <p style='color:#6c757d; font-size:14px;'>You can track this transfer anytime using the tracking number above.</p>";

            return Wrapper("Transfer Sent — Kerzel Pay", body);
        }

        public static string TransferReceived(Transfer transfer, string senderLabel)
        {
            var body = $@"
                <h2 style='color:{AccentColor}; margin-top:0;'>You received money 💰</h2>
                <p>Hi,</p>
                <p>You have received a new transfer.</p>

                <div style='background:#d1e7dd; color:#0f5132; padding:16px; border-radius:6px; text-align:center; margin:20px 0;'>
                    <div style='font-size:28px; font-weight:bold;'>{transfer.DestinationCurrencyCode} {transfer.ConvertedAmount:N2}</div>
                </div>

                <table style='width:100%; border-collapse:collapse; margin:20px 0;'>
                    <tr><td style='padding:8px 0; color:#6c757d;'>From</td>
                        <td style='padding:8px 0; text-align:right;'>{senderLabel}</td></tr>
                    <tr><td style='padding:8px 0; color:#6c757d;'>Tracking number</td>
                        <td style='padding:8px 0; text-align:right;'><code>{transfer.TrackingNumber}</code></td></tr>
                    <tr><td style='padding:8px 0; color:#6c757d;'>Date</td>
                        <td style='padding:8px 0; text-align:right;'>{transfer.CompletedAt:dd MMM yyyy HH:mm}</td></tr>
                </table>";

            return Wrapper("Money Received — Kerzel Pay", body);
        }

        public static string TopUpSuccess(Transfer transfer)
        {
            var body = $@"
                <h2 style='color:{AccentColor}; margin-top:0;'>Top-up successful ✅</h2>
                <p>Hi,</p>
                <p>Your account has been topped up successfully.</p>

                <div style='background:#d1e7dd; color:#0f5132; padding:16px; border-radius:6px; text-align:center; margin:20px 0;'>
                    <div style='font-size:28px; font-weight:bold;'>+{transfer.SourceCurrencyCode} {transfer.Amount:N2}</div>
                </div>

                <p><strong>Tracking number:</strong> <code>{transfer.TrackingNumber}</code></p>
                <p><strong>Date:</strong> {transfer.CompletedAt:dd MMM yyyy HH:mm}</p>";

            return Wrapper("Top-Up Successful — Kerzel Pay", body);
        }

        public static string Welcome(string firstName)
        {
            var body = $@"
                <h2 style='color:{BrandColor}; margin-top:0;'>Welcome to Kerzel Pay, {firstName}! 👋</h2>
                <p>Your account is ready. You can now:</p>
                <ul style='line-height:1.8;'>
                    <li>💳 Create multi-currency accounts</li>
                    <li>💰 Top up via Stripe</li>
                    <li>💸 Send money to anyone, anywhere</li>
                    <li>🔍 Track every transfer in real time</li>
                </ul>
                <p>Welcome aboard!</p>
                <p style='color:#6c757d;'>— The Kerzel Pay team</p>";

            return Wrapper("Welcome to Kerzel Pay", body);
        }
    }
}