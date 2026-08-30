using Marketplace.Web.Auth;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messages;

    public MessagesController(IMessageService messages)
    {
        _messages = messages;
    }

    // GET /api/messages/conversations — yours.
    [HttpGet("conversations")]
    public async Task<ActionResult<List<ConversationSummary>>> GetConversations()
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return await _messages.GetConversationsAsync(me.Value);
    }

    // GET /api/messages/thread?foodDropId=1&otherUserId=2
    // You are always one side of the thread; only the other party is a
    // parameter. Previously both sides were, so any two user ids could be
    // supplied and the whole conversation read back.
    [HttpGet("thread")]
    public async Task<IActionResult> GetThread([FromQuery] int foodDropId, [FromQuery] int otherUserId)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return Ok(await _messages.GetThreadAsync(foodDropId, me.Value, otherUserId));
    }

    public record SendMessageRequest(int FoodDropId, int ReceiverId, string Body);

    // POST /api/messages — the sender is who you are, never what you claim.
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();

        await _messages.SendAsync(request.FoodDropId, me.Value, request.ReceiverId, request.Body);
        return NoContent();
    }
}
