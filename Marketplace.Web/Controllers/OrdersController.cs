using Marketplace.Web.Models;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    public record CreateOrderRequest(int BuyerId, int FoodDropId, int Quantity);

    // POST /api/orders — reserves portions and creates a PendingPayment order.
    // Price/total are computed server-side inside OrderService; nothing here
    // trusts a client-sent amount (Section 39).
    [HttpPost]
    public async Task<ActionResult<Order>> Create([FromBody] CreateOrderRequest request)
    {
        if (request.Quantity < 1)
            return BadRequest("Quantity must be at least 1.");

        var result = await _orders.CreateOrderAsync(request.BuyerId, request.FoodDropId, request.Quantity);
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
        return CreatedAtAction(nameof(GetById), new { id = result.Order!.Id, requestingUserId = request.BuyerId }, result.Order);
    }

    // POST /api/orders/5/pay?buyerId=1 — mocked payment confirmation.
    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<Order>> Pay(int id, [FromQuery] int buyerId)
    {
        var existing = await _orders.GetByIdAsync(id, buyerId);
        if (existing is null) return NotFound();
        if (existing.BuyerId != buyerId) return StatusCode(StatusCodes.Status403Forbidden, "This order isn't yours.");

        var order = await _orders.ConfirmPaymentAsync(id);
        return order is null ? NotFound() : order;
    }

    public record ConfirmPickupRequest(string Code);

    // POST /api/orders/5/pickup?sellerId=2 — seller enters the buyer's 4-digit code.
    [HttpPost("{id:int}/pickup")]
    public async Task<IActionResult> ConfirmPickup(int id, [FromQuery] int sellerId, [FromBody] ConfirmPickupRequest request)
    {
        var result = await _orders.ConfirmPickupAsync(id, sellerId, request.Code);
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

    // GET /api/orders/5?requestingUserId=1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> GetById(int id, [FromQuery] int requestingUserId)
    {
        var order = await _orders.GetByIdAsync(id, requestingUserId);
        return order is null ? NotFound() : order;
    }

    // GET /api/orders/by-buyer/1
    [HttpGet("by-buyer/{buyerId:int}")]
    public async Task<ActionResult<List<Order>>> GetForBuyer(int buyerId) => await _orders.GetForBuyerAsync(buyerId);

    // GET /api/orders/by-seller/2
    [HttpGet("by-seller/{sellerId:int}")]
    public async Task<ActionResult<List<Order>>> GetForSeller(int sellerId) => await _orders.GetForSellerAsync(sellerId);

    // --- Buyer-side lifecycle ---

    // POST /api/orders/5/cancel?buyerId=1 — free until the drop's order deadline.
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelByBuyer(int id, [FromQuery] int buyerId) =>
        ToResponse(await _orders.CancelByBuyerAsync(id, buyerId));

    public record ReviewRequest(int FoodQuality, int Value, int Accuracy, int PickupExperience, string? Comment);

    // POST /api/orders/5/review?buyerId=1 — only ever for a Collected order.
    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, [FromQuery] int buyerId, [FromBody] ReviewRequest request) =>
        ToResponse(await _orders.LeaveReviewAsync(
            id, buyerId, request.FoodQuality, request.Value, request.Accuracy, request.PickupExperience, request.Comment ?? ""));

    // --- Seller-side lifecycle ---

    public record AdvanceRequest(OrderStatus Target);

    // POST /api/orders/5/advance?sellerId=2 — Confirmed -> Preparing -> Ready.
    [HttpPost("{id:int}/advance")]
    public async Task<IActionResult> Advance(int id, [FromQuery] int sellerId, [FromBody] AdvanceRequest request) =>
        ToResponse(await _orders.AdvanceStatusAsync(id, sellerId, request.Target));

    public record SellerCancelRequest(string? Reason);

    // POST /api/orders/5/seller-cancel?sellerId=2 — always a full refund.
    [HttpPost("{id:int}/seller-cancel")]
    public async Task<IActionResult> CancelBySeller(int id, [FromQuery] int sellerId, [FromBody] SellerCancelRequest? request) =>
        ToResponse(await _orders.CancelBySellerAsync(id, sellerId, request?.Reason ?? ""));

    // POST /api/orders/5/no-show?sellerId=2 — only once the pickup window closed.
    [HttpPost("{id:int}/no-show")]
    public async Task<IActionResult> NoShow(int id, [FromQuery] int sellerId) =>
        ToResponse(await _orders.MarkNoShowAsync(id, sellerId));

    // GET /api/orders/drop/7?sellerId=2 — every order against one batch.
    [HttpGet("drop/{foodDropId:int}")]
    public async Task<ActionResult<DropOrderBoard>> DropBoard(int foodDropId, [FromQuery] int sellerId)
    {
        var board = await _orders.GetDropBoardAsync(foodDropId, sellerId);
        return board is null ? NotFound() : board;
    }

    // Forbid() asks the authentication stack to challenge, and this app has no
    // authentication scheme registered — calling it throws
    // InvalidOperationException, so every authorization failure surfaced as a
    // 500 instead of a 403. Return the status code directly instead.
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
