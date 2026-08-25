using Marketplace.Web.Models;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodDropsController : ControllerBase
{
    private readonly IFoodDropService _foodDrops;

    public FoodDropsController(IFoodDropService foodDrops)
    {
        _foodDrops = foodDrops;
    }

    // GET /api/fooddrops?query=curry&categoryId=1&maxPrice=15&dietary=Vegetarian
    [HttpGet]
    public async Task<ActionResult<List<FoodDrop>>> Search(
        [FromQuery] string? query, [FromQuery] int? categoryId, [FromQuery] decimal? maxPrice, [FromQuery] DietaryLabel? dietary)
    {
        return await _foodDrops.SearchAsync(query, categoryId, maxPrice, dietary);
    }

    // GET /api/fooddrops/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FoodDrop>> GetById(int id)
    {
        var drop = await _foodDrops.GetByIdAsync(id);
        return drop is null ? NotFound() : drop;
    }

    // GET /api/fooddrops/by-seller/2
    [HttpGet("by-seller/{sellerId:int}")]
    public async Task<ActionResult<List<FoodDrop>>> GetBySeller(int sellerId)
    {
        return await _foodDrops.GetBySellerAsync(sellerId);
    }

    // POST /api/fooddrops
    [HttpPost]
    public async Task<ActionResult<FoodDrop>> Create([FromBody] FoodDrop drop)
    {
        var created = await _foodDrops.CreateAsync(drop);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    public record CancelDropRequest(string? Reason);

    // POST /api/fooddrops/5/cancel?requestingUserId=1
    // Cancelling a batch refunds every live order against it — see
    // FoodDropService.CancelAsync.
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromQuery] int requestingUserId, [FromBody] CancelDropRequest? request)
    {
        var result = await _foodDrops.CancelAsync(id, requestingUserId, request?.Reason ?? "");
        return ToResponse(result);
    }

    public record SetStageRequest(FoodDropStatus Stage);

    // POST /api/fooddrops/5/stage?requestingUserId=2 — drives the batch through
    // OrderingClosed / Preparing / Ready / Completed, cascading onto live orders.
    [HttpPost("{id:int}/stage")]
    public async Task<IActionResult> SetStage(int id, [FromQuery] int requestingUserId, [FromBody] SetStageRequest request)
    {
        var result = await _foodDrops.SetStageAsync(id, requestingUserId, request.Stage);
        return ToResponse(result);
    }

    // PUT /api/fooddrops/5?requestingUserId=2
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromQuery] int requestingUserId, [FromBody] FoodDrop edited)
    {
        var result = await _foodDrops.UpdateAsync(id, requestingUserId, edited);
        return ToResponse(result);
    }

    // GET /api/fooddrops/kitchen/2 — the cook's own dashboard aggregate.
    [HttpGet("kitchen/{sellerId:int}")]
    public async Task<ActionResult<KitchenSummary>> Kitchen(int sellerId) =>
        await _foodDrops.GetKitchenSummaryAsync(sellerId);

    // Not Forbid(): with no authentication scheme registered it throws rather
    // than returning 403. See the note in OrdersController.
    private IActionResult ToResponse(DropEditResult result) => result.Success
        ? NoContent()
        : result.Error switch
        {
            DropEditError.NotFound => NotFound(),
            DropEditError.NotYours => StatusCode(StatusCodes.Status403Forbidden, "That food drop isn't yours."),
            DropEditError.BelowSoldPortions => Conflict("Portions can't be set below what buyers have already reserved."),
            DropEditError.InvalidWindow => BadRequest("Pickup must end after it starts, and orders must close by the end of the window."),
            DropEditError.Locked => BadRequest("This food drop can no longer be edited."),
            _ => BadRequest(),
        };
}
