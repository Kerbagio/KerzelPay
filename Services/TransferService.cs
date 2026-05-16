using KerzelPay.Data;
using KerzelPay.Helpers;
using KerzelPay.Models;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Services
{
    public class TransferService
    {
        private readonly ApplicationDbContext _db;
        private readonly CurrencyService _currencyService;
        private readonly IEmailService _emailService;

        private async Task<decimal> GetCommissionRateAsync()
        {
            var setting = await _db.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "CommissionPercent");

            if (setting != null && decimal.TryParse(setting.Value, out var rate))
            {
                return rate / 100m;  // stored as "1.5" meaning 1.5%, return 0.015
            }
            return 0.01m;   // fallback: 1%
        }

        public TransferService(
            ApplicationDbContext db,
            CurrencyService currencyService,
            IEmailService emailService)
        {
            _db = db;
            _currencyService = currencyService;
            _emailService = emailService;
        }

        /// <summary>
        /// Account-to-account transfer between two Kerzel Pay accounts.
        /// </summary>
        public async Task<TransferResult> SendToAccountAsync(
            int sourceAccountId,
            string destinationSerial,
            decimal amount,
            string userId,
            string? note)
        {
            // Begin a DB transaction — either everything succeeds or NOTHING does
            using var dbTransaction = await _db.Database.BeginTransactionAsync();

            try
            {
                // Load and lock source account (must belong to current user)
                var sourceAccount = await _db.Accounts
                    .Include(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == sourceAccountId && a.UserId == userId);

                if (sourceAccount == null)
                    return TransferResult.Fail("Source account not found.");

                // Load destination
                var destinationAccount = await _db.Accounts
                    .Include(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.SerialNumber == destinationSerial);

                if (destinationAccount == null)
                    return TransferResult.Fail("Destination account does not exist.");

                if (destinationAccount.Id == sourceAccount.Id)
                    return TransferResult.Fail("You cannot transfer money to the same account.");

                // Calculate commission
                var commissionRate = await GetCommissionRateAsync();
                var commission = Math.Round(amount * commissionRate, 2);
                var totalDebit = amount + commission;

                // Check balance
                if (sourceAccount.Balance < totalDebit)
                    return TransferResult.Fail(
                        $"Insufficient funds. Balance: {sourceAccount.Currency.Symbol}{sourceAccount.Balance:N2}, " +
                        $"required: {sourceAccount.Currency.Symbol}{totalDebit:N2} (amount + 1% commission).");

                // Convert if currencies differ
                var conversion = await _currencyService.ConvertAsync(
                    amount,
                    sourceAccount.Currency.Code,
                    destinationAccount.Currency.Code);

                // Debit source
                sourceAccount.Balance -= totalDebit;

                // Credit destination
                destinationAccount.Balance += conversion.ConvertedAmount;

                // Record the transfer
                var transfer = new Transfer
                {
                    TrackingNumber = SerialNumberGenerator.GenerateTransferTrackingNumber(),
                    Type = TransferType.AccountToAccount,
                    Status = TransferStatus.Completed,
                    SourceAccountId = sourceAccount.Id,
                    DestinationAccountId = destinationAccount.Id,
                    Amount = amount,
                    ConvertedAmount = conversion.ConvertedAmount,
                    ExchangeRate = conversion.ExchangeRate,
                    Commission = commission,
                    SourceCurrencyCode = sourceAccount.Currency.Code,
                    DestinationCurrencyCode = destinationAccount.Currency.Code,
                    Note = note,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };

                _db.Transfers.Add(transfer);

                // Notifications for both parties
                _db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Title = "Transfer sent",
                    Message = $"You sent {sourceAccount.Currency.Symbol}{amount:N2} " +
                              $"to {destinationAccount.SerialNumber}. " +
                              $"Tracking: {transfer.TrackingNumber}"
                });

                _db.Notifications.Add(new Notification
                {
                    UserId = destinationAccount.UserId,
                    Title = "Money received",
                    Message = $"You received {destinationAccount.Currency.Symbol}" +
                              $"{conversion.ConvertedAmount:N2} from {sourceAccount.SerialNumber}. " +
                              $"Tracking: {transfer.TrackingNumber}"
                });

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // Fire-and-forget emails (after commit, so we never send for a rolled-back transfer)
                await SendA2AEmailsAsync(transfer, sourceAccount, destinationAccount);

                return TransferResult.Ok(transfer);
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();  // ❌ something failed, undo everything
                return TransferResult.Fail($"Transfer failed: {ex.Message}");
            }
        }

        /// <summary>
        /// OMT-style transfer to a mobile number (recipient doesn't need a Kerzel Pay account).
        /// Money is debited from sender's account and held by the system until claimed by agent.
        /// For the demo, we simply mark it as Completed.
        /// </summary>
        public async Task<TransferResult> SendToMobileAsync(
            int sourceAccountId,
            string recipientMobile,
            string recipientName,
            decimal amount,
            string userId,
            string? note)
        {
            using var dbTransaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var sourceAccount = await _db.Accounts
                    .Include(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == sourceAccountId && a.UserId == userId);

                if (sourceAccount == null)
                    return TransferResult.Fail("Source account not found.");

                var commissionRate = await GetCommissionRateAsync();
                var commission = Math.Round(amount * commissionRate, 2);
                var totalDebit = amount + commission;

                if (sourceAccount.Balance < totalDebit)
                    return TransferResult.Fail(
                        $"Insufficient funds. Balance: {sourceAccount.Currency.Symbol}{sourceAccount.Balance:N2}, " +
                        $"required: {sourceAccount.Currency.Symbol}{totalDebit:N2}");

                // Debit sender
                sourceAccount.Balance -= totalDebit;

                var transfer = new Transfer
                {
                    TrackingNumber = SerialNumberGenerator.GenerateTransferTrackingNumber(),
                    Type = TransferType.MobileOmt,
                    Status = TransferStatus.Completed,
                    SourceAccountId = sourceAccount.Id,
                    DestinationAccountId = null,
                    RecipientMobile = recipientMobile,
                    RecipientName = recipientName,
                    Amount = amount,
                    ConvertedAmount = amount,  // OMT: same currency as sender
                    ExchangeRate = 1m,
                    Commission = commission,
                    SourceCurrencyCode = sourceAccount.Currency.Code,
                    DestinationCurrencyCode = sourceAccount.Currency.Code,
                    Note = note,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };

                _db.Transfers.Add(transfer);

                _db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Title = "OMT transfer sent",
                    Message = $"You sent {sourceAccount.Currency.Symbol}{amount:N2} to " +
                              $"{recipientName} ({recipientMobile}). Tracking: {transfer.TrackingNumber}"
                });

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                await SendOmtEmailAsync(transfer, sourceAccount);

                return TransferResult.Ok(transfer);
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return TransferResult.Fail($"Transfer failed: {ex.Message}");
            }
        }
        // ----- Email helpers -----

        private async Task SendA2AEmailsAsync(
            Transfer transfer,
            Account sourceAccount,
            Account destinationAccount)
        {
            // Look up sender + receiver users for their emails
            var sender = await _db.Users.FindAsync(sourceAccount.UserId);
            var receiver = await _db.Users.FindAsync(destinationAccount.UserId);

            if (sender?.Email != null)
            {
                var html = EmailTemplates.TransferSent(
                    transfer,
                    destinationAccount.SerialNumber);
                await _emailService.SendAsync(
                    sender.Email,
                    $"Transfer sent — {transfer.TrackingNumber}",
                    html);
            }

            if (receiver?.Email != null)
            {
                var html = EmailTemplates.TransferReceived(
                    transfer,
                    sourceAccount.SerialNumber);
                await _emailService.SendAsync(
                    receiver.Email,
                    $"You received money — {transfer.TrackingNumber}",
                    html);
            }
        }

        private async Task SendOmtEmailAsync(Transfer transfer, Account sourceAccount)
        {
            var sender = await _db.Users.FindAsync(sourceAccount.UserId);

            if (sender?.Email != null)
            {
                var recipientLabel = $"{transfer.RecipientName} ({transfer.RecipientMobile})";
                var html = EmailTemplates.TransferSent(transfer, recipientLabel);
                await _emailService.SendAsync(
                    sender.Email,
                    $"OMT transfer sent — {transfer.TrackingNumber}",
                    html);
            }
        }
    }

    public class TransferResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Transfer? Transfer { get; set; }

        public static TransferResult Ok(Transfer transfer) =>
            new() { Success = true, Transfer = transfer };

        public static TransferResult Fail(string error) =>
            new() { Success = false, ErrorMessage = error };
    }


}