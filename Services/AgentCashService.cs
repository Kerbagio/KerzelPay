using KerzelPay.Data;
using KerzelPay.Helpers;
using KerzelPay.Models;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Services
{
    public class AgentCashService
    {
        private readonly ApplicationDbContext _db;
        public AgentCashService(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Cash-in: customer hands cash to the agent, agent credits their account.
        /// </summary>
        public async Task<AgentCashResult> CashInAsync(
            int agentId,
            string customerAccountSerial,
            decimal amount,
            string? note)
        {
            using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                var agent = await _db.Agents
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.Id == agentId);

                if (agent == null || agent.Status != AgentStatus.Approved)
                    return AgentCashResult.Fail("Agent not authorized.");

                var account = await _db.Accounts
                    .Include(a => a.Currency)
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.SerialNumber == customerAccountSerial);

                if (account == null)
                    return AgentCashResult.Fail("Customer account not found.");

                if (amount <= 0)
                    return AgentCashResult.Fail("Amount must be positive.");

                var commissionRate = await GetAgentCommissionRateAsync();
                var commission = Math.Round(amount * commissionRate, 2);

                // Credit the customer's account
                account.Balance += amount;

                // Add to agent's commission earnings
                agent.TotalCommission += commission;

                // Record the transaction
                var transfer = new Transfer
                {
                    TrackingNumber = SerialNumberGenerator.GenerateTransferTrackingNumber(),
                    Type = TransferType.TopUp,                       // Cash-in is essentially a top-up
                    Status = TransferStatus.Completed,
                    DestinationAccountId = account.Id,
                    Amount = amount,
                    ConvertedAmount = amount,
                    ExchangeRate = 1m,
                    Commission = commission,
                    SourceCurrencyCode = account.Currency.Code,
                    DestinationCurrencyCode = account.Currency.Code,
                    AgentId = agent.Id,
                    Note = $"Cash-in via agent {agent.StoreName}" + (note != null ? $" — {note}" : ""),
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };

                _db.Transfers.Add(transfer);

                // Notify the customer
                _db.Notifications.Add(new Notification
                {
                    UserId = account.UserId,
                    Title = "Cash deposit received 💵",
                    Message = $"You deposited {account.Currency.Symbol}{amount:N2} via agent " +
                              $"{agent.StoreName}. Tracking: {transfer.TrackingNumber}"
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return AgentCashResult.Ok(transfer, commission);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return AgentCashResult.Fail($"Cash-in failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Cash-out: recipient picks up an OMT transfer in cash from an agent.
        /// </summary>
        public async Task<AgentCashResult> CashOutAsync(
            int agentId,
            string trackingNumber,
            string recipientIdProof)
        {
            using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
                if (agent == null || agent.Status != AgentStatus.Approved)
                    return AgentCashResult.Fail("Agent not authorized.");

                var transfer = await _db.Transfers
                    .Include(t => t.SourceAccount)
                    .FirstOrDefaultAsync(t => t.TrackingNumber == trackingNumber);

                if (transfer == null)
                    return AgentCashResult.Fail("Transfer not found. Check the tracking number.");

                if (transfer.Type != TransferType.MobileOmt)
                    return AgentCashResult.Fail("Only OMT mobile transfers can be cashed out.");

                if (transfer.Status != TransferStatus.Pending)
                    return AgentCashResult.Fail($"Transfer is already {transfer.Status}. Cannot cash out.");

                // Calculate agent's commission cut
                var commissionRate = await GetAgentCommissionRateAsync();
                var commission = Math.Round(transfer.Amount * commissionRate, 2);

                // Mark the transfer complete
                transfer.Status = TransferStatus.Completed;
                transfer.CompletedAt = DateTime.UtcNow;
                transfer.AgentId = agent.Id;
                transfer.Note = (transfer.Note ?? "") +
                                $" | Cashed out at {agent.StoreName}, ID verified: {recipientIdProof}";

                // Credit the agent's commission
                agent.TotalCommission += commission;

                // Notify the sender (the cash-out happened)
                if (transfer.SourceAccount != null)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserId = transfer.SourceAccount.UserId,
                        Title = "Your OMT transfer was collected ✅",
                        Message = $"{transfer.RecipientName} picked up " +
                                  $"{transfer.SourceCurrencyCode} {transfer.Amount:N2} at " +
                                  $"{agent.StoreName}. Tracking: {transfer.TrackingNumber}"
                    });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return AgentCashResult.Ok(transfer, commission);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return AgentCashResult.Fail($"Cash-out failed: {ex.Message}");
            }
        }
        private async Task<decimal> GetAgentCommissionRateAsync()
        {
            var setting = await _db.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "AgentCommissionPercent");

            if (setting != null && decimal.TryParse(setting.Value, out var rate))
            {
                return rate / 100m;   // stored as "0.5" meaning 0.5%, return 0.005
            }
            return 0.005m;            // fallback: 0.5%
        }
    }

    public class AgentCashResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Transfer? Transfer { get; set; }
        public decimal Commission { get; set; }

        public static AgentCashResult Ok(Transfer t, decimal commission) =>
            new() { Success = true, Transfer = t, Commission = commission };

        public static AgentCashResult Fail(string error) =>
            new() { Success = false, ErrorMessage = error };
    }
}