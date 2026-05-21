using KerzelPay.Services;
using Microsoft.AspNetCore.Mvc;

namespace KerzelPay.Controllers
{
    [Route("api/chat")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ChatbotService _chatbot;
        private readonly UserContextBuilder _contextBuilder;

        public ChatController(
            ChatbotService chatbot,
            UserContextBuilder contextBuilder)
        {
            _chatbot = chatbot;
            _contextBuilder = contextBuilder;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Message))
                return BadRequest(new { error = "Message cannot be empty." });

            // Build user context if logged in (null if anonymous)
            var userContext = await _contextBuilder.BuildContextAsync(User);

            var reply = await _chatbot.AskAsync(req.Message, req.History, userContext);

            return Ok(new { reply });
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessage>? History { get; set; }
    }
}