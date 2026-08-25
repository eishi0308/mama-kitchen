using Marketplace.Web.Models;
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

    // GET /api/cooks/5/reviews — including the cook's own replies.
    [HttpGet("{cookUserId:int}/reviews")]
    public async Task<ActionResult<List<CookReview>>> Reviews(int cookUserId) =>
        await _sellers.GetReviewsAsync(cookUserId);

    public record ReviewReplyRequest(string Response);

    // POST /api/cooks/5/reviews/12/reply — only the cook who sold the meal.
    [HttpPost("{cookUserId:int}/reviews/{reviewId:int}/reply")]
    public async Task<IActionResult> ReplyToReview(int cookUserId, int reviewId, [FromBody] ReviewReplyRequest request)
    {
        var ok = await _sellers.RespondToReviewAsync(reviewId, cookUserId, request.Response);
        return ok ? NoContent() : StatusCode(StatusCodes.Status403Forbidden, "That review isn't yours to answer.");
    }

    public record PayoutDetailsRequest(string AccountName, string Bsb, string AccountNumber);

    // PUT /api/cooks/5/payout-account — the full account number is reduced to
    // its last four digits and discarded; see SellerService.
    [HttpPut("{cookUserId:int}/payout-account")]
    public async Task<IActionResult> SetPayoutAccount(int cookUserId, [FromBody] PayoutDetailsRequest request)
    {
        var ok = await _sellers.SetPayoutDetailsAsync(cookUserId, request.AccountName, request.Bsb, request.AccountNumber);
        return ok ? NoContent() : BadRequest("Couldn't save those payout details.");
    }

    // POST /api/cooks/5/payouts — cash out everything collected and unpaid.
    [HttpPost("{cookUserId:int}/payouts")]
    public async Task<ActionResult<Payout>> CashOut(int cookUserId)
    {
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
    public async Task<ActionResult<List<Payout>>> Payouts(int cookUserId) =>
        await _sellers.GetPayoutsAsync(cookUserId);

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
