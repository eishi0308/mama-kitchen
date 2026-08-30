using Marketplace.Web.Auth;
using Marketplace.Web.Models;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

// Browsing food is public — that's the shopfront, and requiring a login to see
// what's for dinner would be the wrong trade. Everything that *changes* a batch
// requires the signed-in cook who owns it.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FoodDropsController : ControllerBase
{
    private readonly IFoodDropService _foodDrops;

    public FoodDropsController(IFoodDropService foodDrops)
    {
        _foodDrops = foodDrops;
    }

    // GET /api/fooddrops?query=curry&categoryId=1&maxPrice=15&dietary=Vegetarian
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<FoodDrop>>> Search(
        [FromQuery] string? query, [FromQuery] int? categoryId, [FromQuery] decimal? maxPrice, [FromQuery] DietaryLabel? dietary)
    {
        return await _foodDrops.SearchAsync(query, categoryId, maxPrice, dietary);
    }

    // GET /api/fooddrops/5
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FoodDrop>> GetById(int id)
    {
        var drop = await _foodDrops.GetByIdAsync(id);
        return drop is null ? NotFound() : drop;
    }

    // GET /api/fooddrops/by-seller/2
    [AllowAnonymous]
    [HttpGet("by-seller/{sellerId:int}")]
    public async Task<ActionResult<List<FoodDrop>>> GetBySeller(int sellerId)
    {
        return await _foodDrops.GetBySellerAsync(sellerId);
    }

    // POST /api/fooddrops
    [HttpPost]
    public async Task<ActionResult<FoodDrop>> Create([FromBody] FoodDrop drop)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();

        // Overwritten, not validated. The body is a full FoodDrop entity, so a
        // client could otherwise set SellerId to someone else and post a batch
        // in their name — with their pickup address attached to it.
        drop.SellerId = me.Value;

        var created = await _foodDrops.CreateAsync(drop);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    public record CancelDropRequest(string? Reason);

    // POST /api/fooddrops/5/cancel
    // Cancelling a batch refunds every live order against it — see
    // FoodDropService.CancelAsync.
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelDropRequest? request)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return ToResponse(await _foodDrops.CancelAsync(id, me.Value, request?.Reason ?? ""));
    }

    public record SetStageRequest(FoodDropStatus Stage);

    // POST /api/fooddrops/5/stage — drives the batch through
    // OrderingClosed / Preparing / Ready / Completed, cascading onto live orders.
    [HttpPost("{id:int}/stage")]
    public async Task<IActionResult> SetStage(int id, [FromBody] SetStageRequest request)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return ToResponse(await _foodDrops.SetStageAsync(id, me.Value, request.Stage));
    }

    // PUT /api/fooddrops/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FoodDrop edited)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return ToResponse(await _foodDrops.UpdateAsync(id, me.Value, edited));
    }

    // GET /api/fooddrops/kitchen — your own dashboard aggregate. The seller id
    // used to be a route segment, which published every cook's operational
    // numbers to anyone who guessed an id.
    [HttpGet("kitchen")]
    public async Task<ActionResult<KitchenSummary>> Kitchen()
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return await _foodDrops.GetKitchenSummaryAsync(me.Value);
    }

    // 403 directly rather than Forbid(), which for cookie auth redirects to
    // /login — right for a browser, wrong for an API client.
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
