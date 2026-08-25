using Marketplace.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

// The seller half of the public API. Named "cooks" rather than "sellers"
// because that's the word the product uses everywhere a person can see it.
[ApiController]
[Route("api/[controller]")]
public class CooksController : ControllerBase
{
    private readonly ISellerService _sellers;

    public CooksController(ISellerService sellers)
    {
        _sellers = sellers;
    }

    // GET /api/cooks/5 — public profile: story, verification, drops, reviews.
    // Contains nothing private; exact pickup addresses live on PickupLocation
    // and are only ever surfaced through a confirmed order.
    [HttpGet("{cookUserId:int}")]
    public async Task<ActionResult<CookPublicProfile>> Get(int cookUserId)
    {
        var profile = await _sellers.GetPublicProfileAsync(cookUserId);
        return profile is null ? NotFound() : profile;
    }

    // GET /api/cooks/5/earnings — the cook's own money. Real auth would scope
    // this to the signed-in user; the demo has no auth to scope against.
    [HttpGet("{cookUserId:int}/earnings")]
    public async Task<ActionResult<SellerEarnings>> Earnings(int cookUserId) =>
        await _sellers.GetEarningsAsync(cookUserId);

    public record OnboardRequest(
        string Suburb, string Cuisine, string Story,
        string LocationLabel, string ExactAddress, string Instructions, double ApproxDistanceKm);

    // POST /api/cooks?userId=1 — turns a buyer-only account into a cook.
    [HttpPost]
    public async Task<IActionResult> Onboard([FromQuery] int userId, [FromBody] OnboardRequest request)
    {
        var profile = await _sellers.CreateProfileAsync(userId, new SellerOnboardingRequest(
            request.Suburb, request.Cuisine, request.Story,
            request.LocationLabel, request.ExactAddress, request.Instructions, request.ApproxDistanceKm));
        return CreatedAtAction(nameof(Get), new { cookUserId = userId }, profile);
    }
}
