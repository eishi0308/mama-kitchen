using Marketplace.Web.Auth;
using Marketplace.Web.Models;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

// Every endpoint here acts *as somebody*, so the whole controller requires a
// signed-in caller and the acting user is always read from the auth cookie.
// It used to be a query parameter — `?sellerId=2` — which meant anyone could
// confirm anyone's pickup, cancel anyone's order, or read anyone's history.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    public record CreateOrderRequest(int FoodDropId, int Quantity);

    // POST /api/orders — reserves portions and creates a PendingPayment order.
    // Price/total are computed server-side inside OrderService; nothing here
    // trusts a client-sent amount (Section 39).
    [HttpPost]
    public async Task<ActionResult<Order>> Create([FromBody] CreateOrderRequest request)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();

        if (request.Quantity < 1)
            return BadRequest("Quantity must be at least 1.");

        var result = await _orders.CreateOrderAsync(me.Value, request.FoodDropId, request.Quantity);
        if (!result.Success)
        {
            return result.Error switch
            {
                OrderCreationError.FoodDropNotFound => NotFound("Food drop not found."),
                OrderCreationError.CannotOrderOwnListing => BadRequest("You can't order your own food drop."),
                OrderCreationError.NotOrderable => BadRequest("This food drop isn't taking orders right now."),
                OrderCreationError.InsufficientPortions => Conflict("Not enough portions left."),
                _ => BadRequest(),
            };
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Order!.Id }, result.Order);
    }

    // POST /api/orders/5/pay — mocked payment confirmation.
    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<Order>> Pay(int id)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();

        var existing = await _orders.GetByIdAsync(id, me.Value);
        if (existing is null) return NotFound();
        if (existing.BuyerId != me.Value)
            return StatusCode(StatusCodes.Status403Forbidden, "This order isn't yours.");

        var order = await _orders.ConfirmPaymentAsync(id);
        return order is null ? NotFound() : order;
    }

    public record ConfirmPickupRequest(string Code);

    // POST /api/orders/5/pickup — the cook enters the buyer's 4-digit code.
    [HttpPost("{id:int}/pickup")]
    public async Task<IActionResult> ConfirmPickup(int id, [FromBody] ConfirmPickupRequest request)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();

        var result = await _orders.ConfirmPickupAsync(id, me.Value, request.Code);
        if (result.Success) return NoContent();

        return result.Error switch
        {
            PickupConfirmError.OrderNotFound => NotFound(),
            PickupConfirmError.NotOwnerOfFoodDrop => Denied("That food drop isn't yours."),
            PickupConfirmError.NotConfirmed => BadRequest("This order isn't ready for pickup."),
            PickupConfirmError.CodeMismatch => BadRequest("That code doesn't match."),
            _ => BadRequest(),
        };
    }

    // GET /api/orders/5 — scoped to you by OrderService, which returns null for
    // an order you're neither the buyer nor the cook on.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> GetById(int id)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();

        var order = await _orders.GetByIdAsync(id, me.Value);
        return order is null ? NotFound() : order;
    }

    // GET /api/orders/mine — what you've bought.
    [HttpGet("mine")]
    public async Task<ActionResult<List<Order>>> GetMine()
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return await _orders.GetForBuyerAsync(me.Value);
    }

    // GET /api/orders/selling — what people have bought from you.
    [HttpGet("selling")]
    public async Task<ActionResult<List<Order>>> GetSelling()
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return await _orders.GetForSellerAsync(me.Value);
    }

    // --- Buyer-side lifecycle ---

    // POST /api/orders/5/cancel — free until the drop's order deadline.
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelByBuyer(int id)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return ToResponse(await _orders.CancelByBuyerAsync(id, me.Value));
    }

    public record ReviewRequest(int FoodQuality, int Value, int Accuracy, int PickupExperience, string? Comment);

    // POST /api/orders/5/review — only ever for a Collected order.
    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewRequest request)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return ToResponse(await _orders.LeaveReviewAsync(
            id, me.Value, request.FoodQuality, request.Value, request.Accuracy,
            request.PickupExperience, request.Comment ?? ""));
    }

    // --- Seller-side lifecycle ---

    public record AdvanceRequest(OrderStatus Target);

    // POST /api/orders/5/advance — Confirmed -> Preparing -> Ready.
    [HttpPost("{id:int}/advance")]
    public async Task<IActionResult> Advance(int id, [FromBody] AdvanceRequest request)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return ToResponse(await _orders.AdvanceStatusAsync(id, me.Value, request.Target));
    }

    public record SellerCancelRequest(string? Reason);

    // POST /api/orders/5/seller-cancel — always a full refund.
    [HttpPost("{id:int}/seller-cancel")]
    public async Task<IActionResult> CancelBySeller(int id, [FromBody] SellerCancelRequest? request)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return ToResponse(await _orders.CancelBySellerAsync(id, me.Value, request?.Reason ?? ""));
    }

    // POST /api/orders/5/no-show — only once the pickup window closed.
    [HttpPost("{id:int}/no-show")]
    public async Task<IActionResult> NoShow(int id)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return ToResponse(await _orders.MarkNoShowAsync(id, me.Value));
    }

    // GET /api/orders/drop/7 — every order against one of your batches.
    [HttpGet("drop/{foodDropId:int}")]
    public async Task<ActionResult<DropOrderBoard>> DropBoard(int foodDropId)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();

        var board = await _orders.GetDropBoardAsync(foodDropId, me.Value);
        return board is null ? NotFound() : board;
    }

    // Returns 403 directly rather than calling Forbid(). Forbid() runs the
    // scheme's challenge, which for cookie auth is a 302 to /login — correct
    // for a browser, wrong for an API client that wants a status code.
    private IActionResult Denied(string message) => StatusCode(StatusCodes.Status403Forbidden, message);

    private IActionResult ToResponse(OrderActionResult result) => result.Success
        ? NoContent()
        : result.Error switch
        {
            OrderActionError.OrderNotFound => NotFound(),
            OrderActionError.NotYours => Denied("That order isn't yours."),
            OrderActionError.TooLateToCancel => Conflict("Orders for this drop have already closed."),
            OrderActionError.AlreadyReviewed => Conflict("This order has already been reviewed."),
            OrderActionError.NotCollected => BadRequest("You can only review an order you collected."),
            OrderActionError.InvalidRating => BadRequest("Every rating must be between 1 and 5."),
            OrderActionError.PickupWindowNotOver => BadRequest("The pickup window hasn't closed yet."),
            OrderActionError.RefundFailed => StatusCode(502, "The refund could not be processed."),
            _ => BadRequest(),
        };
}
