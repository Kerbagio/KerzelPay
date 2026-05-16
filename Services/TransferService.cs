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

        // Flat commission per transfer (in source currency)
        // Could be made admin-configurable in Session 11
        private const decimal COMMISSION_PERCENT = 0.01m; // 1%

        public TransferService(ApplicationDbContext db, CurrencyService currencyService)
        {
            _db = db;
            _currencyService = currencyService;
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
                var commission = Math.Round(amount * COMMISSION_PERCENT, 2);
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
                await dbTransaction.CommitAsync();   // ✅ all good, commit

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

                var commission = Math.Round(amount * COMMISSION_PERCENT, 2);
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

                return TransferResult.Ok(transfer);
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return TransferResult.Fail($"Transfer failed: {ex.Message}");
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