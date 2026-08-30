using System.Security.Claims;
using Marketplace.Web.Services;

namespace Marketplace.Web.Auth;

public static class ClaimsPrincipalExtensions
{
    /// The caller's own user id, straight from the auth cookie.
    ///
    /// Every controller that used to take the acting user's id as a parameter
    /// now reads it from here instead. That parameter was a demo shortcut with
    /// a note in the README saying real auth would supply it — and as an
    /// authenticated API it was an impersonation hole: `POST
    /// /api/orders/5/pickup?sellerId=7` let anyone confirm anyone's handover.
    public static int? AppUserId(this ClaimsPrincipal principal) =>
        UserAccountService.GetUserId(principal);
}
