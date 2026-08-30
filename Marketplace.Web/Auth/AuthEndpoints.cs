using System.Security.Claims;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Web.Auth;

/// Sign-in and sign-out live on plain HTTP endpoints rather than inside Blazor
/// components, and they have to.
///
/// Writing an auth cookie means writing a response header. An interactive
/// Blazor Server component runs over an already-established SignalR circuit —
/// the response headers went out long ago, and HttpContext.SignInAsync would
/// throw. So every path that changes who you are is a real navigation to one of
/// these endpoints, which then redirects back into the app.
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/auth");

        // --- Google ---------------------------------------------------------
        // A GET that starts an OAuth challenge. Safe as a GET: the handler
        // generates a state parameter and a PKCE verifier, so a forged link
        // can't complete a sign-in on someone else's behalf.
        auth.MapGet("/login/google", (HttpContext http, string? returnUrl) =>
        {
            if (!GoogleIsConfigured(http))
            {
                return Results.Redirect("/login?error=google-not-configured");
            }

            var properties = new AuthenticationProperties
            {
                RedirectUri = SafeReturnUrl(returnUrl),
            };
            return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
        });

        // --- Demo accounts --------------------------------------------------
        // POST, and antiforgery-protected by the framework, because this one
        // really does change who you are on a GET-able URL otherwise. The flag
        // check is the important part: it means this endpoint can only ever
        // produce a seeded account, never a real person's.
        // Bound [FromForm], which is what switches on ASP.NET Core's automatic
        // antiforgery validation for minimal APIs — a query-bound POST would
        // skip it silently and leave this forgeable.
        auth.MapPost("/demo", async (
            HttpContext http,
            Marketplace.Web.Data.AppDbContext db,
            [FromForm] int userId,
            [FromForm] string? returnUrl) =>
        {
            var user = await db.Users.FindAsync(userId);
            if (user is null || !user.IsDemo)
            {
                // Deliberately vague, and deliberately not "no such user":
                // this endpoint should never confirm whether a real account id
                // exists.
                return Results.Redirect("/login?error=not-a-demo-account");
            }

            user.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var principal = UserAccountService.BuildPrincipal(
                user, CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Results.Redirect(SafeReturnUrl(returnUrl));
        });

        // --- Sign out -------------------------------------------------------
        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });
    }

    private static bool GoogleIsConfigured(HttpContext http)
    {
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        return !string.IsNullOrWhiteSpace(config["Authentication:Google:ClientId"]);
    }

    /// Only ever redirect to a path on this site. Without this check the
    /// returnUrl parameter is an open redirect: a link to our own trusted
    /// domain that quietly lands the user on someone else's, which is exactly
    /// the shape a credible phishing page needs.
    internal static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return "/";

        // Must be a single-slash-rooted relative path. "//evil.com" and
        // "/\evil.com" are both protocol-relative URLs in a browser, and
        // "https://evil.com" is obviously absolute.
        if (!returnUrl.StartsWith('/')) return "/";
        if (returnUrl.StartsWith("//") || returnUrl.StartsWith("/\\")) return "/";

        return returnUrl;
    }

    /// Called by the Google handler once it has a validated ticket. This is
    /// where a Google identity becomes a Batch account.
    public static async Task OnGoogleTicketReceived(TicketReceivedContext context)
    {
        var accounts = context.HttpContext.RequestServices.GetRequiredService<UserAccountService>();

        var subject = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            // No subject means no stable identity to key on — refuse rather
            // than inventing one.
            context.Fail("Google did not return a subject identifier.");
            return;
        }

        var user = await accounts.FindOrCreateFromGoogleAsync(
            subject,
            context.Principal?.FindFirst(ClaimTypes.Email)?.Value,
            context.Principal?.FindFirst(ClaimTypes.Name)?.Value,
            context.Principal?.FindFirst("picture")?.Value);

        // Replace Google's principal with our own before the cookie is written,
        // so the cookie carries our user id and nothing else from Google.
        context.Principal = UserAccountService.BuildPrincipal(
            user, CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
