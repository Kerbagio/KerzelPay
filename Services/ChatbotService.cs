using System.Text;
using System.Text.Json;

namespace KerzelPay.Services
{
    public class ChatbotService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<ChatbotService> _logger;

        // System prompt — teaches Gemini about Kerzel Pay
        private const string SYSTEM_PROMPT = @"
You are the customer support assistant for Kerzel Pay, a Lebanese money transfer web application.

ABOUT KERZEL PAY:
- Users can create multi-currency accounts (USD, EUR, GBP, LBP)
- Each account has a unique serial number like KP-2026-XXXXXX
- Top-up via Stripe (test mode currently)
- Send money to other Kerzel Pay accounts (Account-to-Account) OR to a mobile number (OMT-style pickup)
- Live currency conversion using European Central Bank rates
- 1% platform commission on transfers (admin-configurable)
- Agents are partner stores; they handle cash-in (customer deposits cash) and cash-out (recipient picks up OMT transfer)
- 0.5% agent commission on cash operations (admin-configurable)
- OMT transfers stay Pending until collected at any approved agent
- Recipients need a tracking number (TRX-YYYYMMDD-XXXXXX) and ID to collect cash
- Public tracking: anyone can look up a transfer by tracking number, no login needed
- Sign in with email OR Google OAuth
- Real email notifications for transfers and top-ups

GUIDELINES:
- Be friendly, concise, and helpful — like a real support agent
- Answer in 2-4 sentences max unless they need step-by-step
- If asked about technical details outside Kerzel Pay (general programming, weather, etc.), politely redirect to Kerzel Pay topics
- If you don't know an answer, say 'I'll have a human agent follow up' — never invent features
- Use simple language; many users may not be tech-savvy
- For sensitive issues (lost money, fraud), tell them to contact support@kerzelpay.com
";

        public ChatbotService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<ChatbotService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        public async Task<string> AskAsync(
    string userMessage,
    List<ChatMessage>? history = null,
    string? userContext = null)
        {
            try
            {
                var apiKey = _config["Gemini:ApiKey"];
                var model = _config["Gemini:Model"] ?? "gemini-2.5-flash";

                if (string.IsNullOrEmpty(apiKey))
                    return "Chatbot is temporarily unavailable. Please contact support@kerzelpay.com.";

                var client = _httpClientFactory.CreateClient("Gemini");

                // Build the conversation history
                var contents = new List<object>();

                if (history != null)
                {
                    foreach (var msg in history)
                    {
                        contents.Add(new
                        {
                            role = msg.IsUser ? "user" : "model",
                            parts = new[] { new { text = msg.Text } }
                        });
                    }
                }

                // Add the new user message
                contents.Add(new
                {
                    role = "user",
                    parts = new[] { new { text = userMessage } }
                });

                var payload = new
                {
                    system_instruction = new
                    {
                        parts = new[]
    {
        new { text = SYSTEM_PROMPT },
        new { text = userContext ?? "(User is not logged in. Answer general questions only.)" }
    }
                    },
                    contents = contents,
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 500
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"v1beta/models/{model}:generateContent?key={apiKey}";
                var response = await client.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Gemini API returned {Status}: {Body}",
                        response.StatusCode, errorBody);
                    return "I'm having trouble connecting right now. Please try again in a moment.";
                }

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);

                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text ?? "I'm not sure how to answer that. Could you rephrase?";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Network error calling Gemini: {Message}", ex.Message);
                return "I can't reach my AI service right now. Please try again in a moment.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in chatbot");
                return "Something went wrong. Please contact support@kerzelpay.com if this continues.";
            }
        }
    }

    public class ChatMessage
    {
        public string Text { get; set; } = string.Empty;
        public bool IsUser { get; set; }
    }
}