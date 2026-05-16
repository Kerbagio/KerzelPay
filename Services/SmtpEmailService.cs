using System.Net;
using System.Net.Mail;

namespace KerzelPay.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var host = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["Email:SmtpPort"] ?? "587");
                var fromEmail = _config["Email:FromEmail"]!;
                var fromName = _config["Email:FromName"] ?? "Kerzel Pay";
                var username = _config["Email:Username"]!;
                var password = _config["Email:Password"]!;

                using var smtp = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                await smtp.SendMailAsync(message);

                _logger.LogInformation("Email sent to {Email} — subject: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                // Log but never throw — email failures must NOT break transfers
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            }
        }
    }
}