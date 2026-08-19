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
        if (existing.BuyerId != buyerId) return Forbid();

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
            PickupConfirmError.NotOwnerOfFoodDrop => Forbid(),
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
}
