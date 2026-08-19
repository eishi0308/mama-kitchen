using Marketplace.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messages;

    public MessagesController(IMessageService messages)
    {
        _messages = messages;
    }

    // GET /api/messages/conversations/1
    [HttpGet("conversations/{userId:int}")]
    public async Task<ActionResult<List<ConversationSummary>>> GetConversations(int userId) =>
        await _messages.GetConversationsAsync(userId);

    // GET /api/messages/thread?foodDropId=1&userId=1&otherUserId=2
    [HttpGet("thread")]
    public async Task<IActionResult> GetThread([FromQuery] int foodDropId, [FromQuery] int userId, [FromQuery] int otherUserId) =>
        Ok(await _messages.GetThreadAsync(foodDropId, userId, otherUserId));

    public record SendMessageRequest(int FoodDropId, int SenderId, int ReceiverId, string Body);

    // POST /api/messages
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request)
    {
        await _messages.SendAsync(request.FoodDropId, request.SenderId, request.ReceiverId, request.Body);
        return NoContent();
    }
}
