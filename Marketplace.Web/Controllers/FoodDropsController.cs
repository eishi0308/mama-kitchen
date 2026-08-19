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

    // POST /api/fooddrops/5/cancel?requestingUserId=1
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromQuery] int requestingUserId)
    {
        var ok = await _foodDrops.CancelAsync(id, requestingUserId);
        return ok ? NoContent() : Forbid();
    }
}
