using Marketplace.Web.Models;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IFoodDropService _foodDrops;

    public CategoriesController(IFoodDropService foodDrops)
    {
        _foodDrops = foodDrops;
    }

    [HttpGet]
    public async Task<ActionResult<List<Category>>> GetAll() => await _foodDrops.GetCategoriesAsync();
}
