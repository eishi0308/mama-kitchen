using Marketplace.Web.Auth;
using Marketplace.Web.Models;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

// The seller half of the public API. Named "cooks" rather than "sellers"
// because that's the word the product uses everywhere a person can see it.
//
// The public profile and reviews stay anonymous — they're the shopfront. The
// money is not: earnings, payouts and payout accounts are readable only by the
// cook they belong to.
[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    [AllowAnonymous]
    [HttpGet("{cookUserId:int}")]
    public async Task<ActionResult<CookPublicProfile>> Get(int cookUserId)
    {
        var profile = await _sellers.GetPublicProfileAsync(cookUserId);
        return profile is null ? NotFound() : profile;
    }

    // GET /api/cooks/5/reviews — including the cook's own replies.
    [AllowAnonymous]
    [HttpGet("{cookUserId:int}/reviews")]
    public async Task<ActionResult<List<CookReview>>> Reviews(int cookUserId) =>
        await _sellers.GetReviewsAsync(cookUserId);

    // GET /api/cooks/5/earnings — the cook's own money, and nobody else's.
    // The route keeps the id so the URLs stay stable, but the id is now checked
    // against the caller instead of trusted.
    [HttpGet("{cookUserId:int}/earnings")]
    public async Task<ActionResult<SellerEarnings>> Earnings(int cookUserId)
    {
        if (NotMe(cookUserId, out var denied)) return denied!;
        return await _sellers.GetEarningsAsync(cookUserId);
    }

    public record ReviewReplyRequest(string Response);

    // POST /api/cooks/5/reviews/12/reply — only the cook who sold the meal.
    [HttpPost("{cookUserId:int}/reviews/{reviewId:int}/reply")]
    public async Task<IActionResult> ReplyToReview(int cookUserId, int reviewId, [FromBody] ReviewReplyRequest request)
    {
        if (NotMe(cookUserId, out var denied)) return denied!;

        var ok = await _sellers.RespondToReviewAsync(reviewId, cookUserId, request.Response);
        return ok ? NoContent() : StatusCode(StatusCodes.Status403Forbidden, "That review isn't yours to answer.");
    }

    public record PayoutDetailsRequest(string AccountName, string Bsb, string AccountNumber);

    // PUT /api/cooks/5/payout-account — the full account number is reduced to
    // its last four digits and discarded; see SellerService.
    [HttpPut("{cookUserId:int}/payout-account")]
    public async Task<IActionResult> SetPayoutAccount(int cookUserId, [FromBody] PayoutDetailsRequest request)
    {
        if (NotMe(cookUserId, out var denied)) return denied!;

        var ok = await _sellers.SetPayoutDetailsAsync(cookUserId, request.AccountName, request.Bsb, request.AccountNumber);
        return ok ? NoContent() : BadRequest("Couldn't save those payout details.");
    }

    // POST /api/cooks/5/payouts — cash out everything collected and unpaid.
    [HttpPost("{cookUserId:int}/payouts")]
    public async Task<ActionResult<Payout>> CashOut(int cookUserId)
    {
        if (NotMe(cookUserId, out var denied)) return denied!;

        var result = await _sellers.RequestPayoutAsync(cookUserId);
        if (result.Success) return result.Payout!;

        return result.Error switch
        {
            PayoutError.NoProfile => NotFound("That user doesn't have a kitchen."),
            PayoutError.NoPayoutMethod => BadRequest("No payout account has been set up."),
            PayoutError.NothingToPayOut => Conflict("There's nothing waiting to be paid out."),
            PayoutError.BelowMinimum => Conflict($"Minimum payout is ${SellerService.MinimumPayout:0.00}."),
            PayoutError.TransferFailed => StatusCode(502, "The transfer could not be completed."),
            _ => BadRequest(),
        };
    }

    // GET /api/cooks/5/payouts
    [HttpGet("{cookUserId:int}/payouts")]
    public async Task<ActionResult<List<Payout>>> Payouts(int cookUserId)
    {
        if (NotMe(cookUserId, out var denied)) return denied!;
        return await _sellers.GetPayoutsAsync(cookUserId);
    }

    public record OnboardRequest(
        string Suburb, string Cuisine, string Story,
        string LocationLabel, string ExactAddress, string Instructions, double ApproxDistanceKm);

    // POST /api/cooks — turns your own account into a cook. The user id used to
    // come from the query string, which meant you could open a kitchen in
    // someone else's name.
    [HttpPost]
    public async Task<IActionResult> Onboard([FromBody] OnboardRequest request)
    {
        var me = User.AppUserId();
        if (me is null) return Unauthorized();

        var profile = await _sellers.CreateProfileAsync(me.Value, new SellerOnboardingRequest(
            request.Suburb, request.Cuisine, request.Story,
            request.LocationLabel, request.ExactAddress, request.Instructions, request.ApproxDistanceKm));
        return CreatedAtAction(nameof(Get), new { cookUserId = me.Value }, profile);
    }

    /// True when the route's cook isn't the caller. 404 rather than 403 on
    /// mismatch: "403 on someone else's id" confirms that the id exists, which
    /// turns this into an account-enumeration oracle.
    private bool NotMe(int cookUserId, out ActionResult? denied)
    {
        var me = User.AppUserId();
        if (me is null) { denied = Unauthorized(); return true; }
        if (me.Value != cookUserId) { denied = NotFound(); return true; }
        denied = null;
        return false;
    }
}
