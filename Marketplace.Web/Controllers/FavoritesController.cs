using Marketplace.Web.Models;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favorites;

    public FavoritesController(IFavoriteService favorites)
    {
        _favorites = favorites;
    }

    // GET /api/favorites/1
    [HttpGet("{userId:int}")]
    public async Task<ActionResult<List<FoodDrop>>> GetAll(int userId) => await _favorites.GetFavoritesAsync(userId);

    // POST /api/favorites/toggle?userId=1&foodDropId=3
    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromQuery] int userId, [FromQuery] int foodDropId)
    {
        await _favorites.ToggleAsync(userId, foodDropId);
        return NoContent();
    }
}
