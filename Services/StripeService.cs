using Stripe.Checkout;

namespace KerzelPay.Services
{
    public class StripeService
    {
        public async Task<Session> CreateCheckoutSessionAsync(
            decimal amount,
            string currencyCode,
            int accountId,
            string successUrl,
            string cancelUrl)
        {
            // Stripe expects the amount in the "smallest unit" (cents for USD/EUR/GBP)
            // LBP has no decimal subunit, so multiplier differs by currency.
            var stripeAmount = ToStripeAmount(amount, currencyCode);

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = stripeAmount,
                            Currency = currencyCode.ToLower(),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Kerzel Pay — Account Top Up",
                                Description = $"Top up your {currencyCode} account"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = cancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "account_id", accountId.ToString() },
                    { "original_amount", amount.ToString("F2") },
                    { "currency_code", currencyCode }
                }
            };

            var service = new SessionService();
            return await service.CreateAsync(options);
        }

        public async Task<Session> GetSessionAsync(string sessionId)
        {
            var service = new SessionService();
            return await service.GetAsync(sessionId);
        }

        private static long ToStripeAmount(decimal amount, string currencyCode)
        {
            // Zero-decimal currencies in Stripe (no cents)
            var zeroDecimal = new HashSet<string> { "LBP", "JPY", "KRW", "VND" };

            if (zeroDecimal.Contains(currencyCode.ToUpper()))
            {
                return (long)amount;
            }

            return (long)(amount * 100);  // cents
        }
    }
}