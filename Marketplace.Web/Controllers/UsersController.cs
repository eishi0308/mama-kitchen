using Marketplace.Web.Auth;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserAccountService _accounts;

    public UsersController(UserAccountService accounts)
    {
        _accounts = accounts;
    }

    /// The signed-in user, and only the signed-in user.
    ///
    /// This used to be `GET /api/users` returning `_db.Users.ToListAsync()` —
    /// every row, unauthenticated. That was harmless when every row was a demo
    /// account; the moment real people sign in it publishes their names, email
    /// addresses and Google subject ids to anyone who curls the endpoint.
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var id = User.AppUserId();
        if (id is null) return Unauthorized();

        var user = await _accounts.LoadAsync(id.Value);
        if (user is null) return Unauthorized();

        return new CurrentUserResponse(
            user.Id, user.Name, user.Avatar, user.Email,
            user.PictureUrl, user.IsDemo, user.SellerProfile is not null);
    }

    /// Deliberately a projection, not the entity: returning the User row would
    /// serialise GoogleSubjectId straight out to the client.
    public record CurrentUserResponse(
        int Id, string Name, string Avatar, string? Email,
        string? PictureUrl, bool IsDemo, bool IsCook);
}
