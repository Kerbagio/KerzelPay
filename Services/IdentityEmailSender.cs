using Microsoft.AspNetCore.Identity.UI.Services;

namespace KerzelPay.Services
{
    /// <summary>
    /// Adapter so ASP.NET Identity's password reset / email change features
    /// use our existing SmtpEmailService.
    /// </summary>
    public class IdentityEmailSender : IEmailSender
    {
        private readonly IEmailService _emailService;

        public IdentityEmailSender(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Wrap the raw HTML in our branded template
            var branded = $@"
<!DOCTYPE html>
<html>
<body style='margin:0; padding:0; font-family: -apple-system, BlinkMacSystemFont, sans-serif; background:#f4f4f7;'>
    <div style='max-width:600px; margin:30px auto; background:#fff; border-radius:8px; overflow:hidden; box-shadow:0 2px 4px rgba(0,0,0,0.05);'>
        <div style='background:#0d6efd; color:#fff; padding:24px; text-align:center;'>
            <h1 style='margin:0; font-size:24px;'>💸 Kerzel Pay</h1>
        </div>
        <div style='padding:32px;'>
            {htmlMessage}
        </div>
        <div style='background:#f8f9fa; padding:16px; text-align:center; color:#6c757d; font-size:12px;'>
            This is an automated message from Kerzel Pay.<br/>
            If you didn't request this, you can safely ignore this email.
        </div>
    </div>
</body>
</html>";

            return _emailService.SendAsync(email, subject, branded);
        }
    }
}