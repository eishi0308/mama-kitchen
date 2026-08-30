using Marketplace.Web.Auth;
using Marketplace.Web.Models;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favorites;

    public FavoritesController(IFavoriteService favorites)
    {
        _favorites = favorites;
    }

    // GET /api/favorites — your saved drops. The user id used to be a route
    // segment, which made everyone's saved list world-readable.
    [HttpGet]
    public async Task<ActionResult<List<FoodDrop>>> GetAll()
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();
        return await _favorites.GetFavoritesAsync(me.Value);
    }

    // POST /api/favorites/toggle?foodDropId=3
    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromQuery] int foodDropId)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();

        await _favorites.ToggleAsync(me.Value, foodDropId);
        return NoContent();
    }
}
