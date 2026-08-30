using System.Security.Claims;
using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

/// Turns an external identity (a Google ticket) into a row in our Users table,
/// and turns a signed-in ClaimsPrincipal back into that row.
///
/// This is the whole of "registration". With an OAuth-only app there is no
/// signup form to fill in: the first time someone signs in with Google we
/// already know their name, email and picture, so the account is created
/// just-in-time and they land in the app instead of on a form.
public class UserAccountService
{
    /// Our own claim, holding the primary key of the Users row. Everything in
    /// the app keys off this rather than off Google's subject id, so demo
    /// accounts — which have no Google identity — are signed in exactly the
    /// same way and every downstream lookup stays identical.
    public const string AppUserIdClaim = "batch:uid";

    private readonly AppDbContext _db;

    public UserAccountService(AppDbContext db) => _db = db;

    /// Find-or-create the User behind a Google sign-in.
    ///
    /// Matched on the Google subject id only. Matching on email would let
    /// someone who acquires a recycled Google address inherit the previous
    /// owner's orders, payouts and reviews.
    public async Task<User> FindOrCreateFromGoogleAsync(
        string googleSubjectId, string? email, string? name, string? pictureUrl)
    {
        var user = await _db.Users
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId);

        if (user is null)
        {
            user = new User
            {
                GoogleSubjectId = googleSubjectId,
                Email = email,
                // Google always sends a name for a consumer account, but the
                // scope can be declined; falling back to the email's local part
                // beats showing an empty sidebar.
                Name = FirstNonEmpty(name, LocalPart(email), "New neighbour"),
                PictureUrl = pictureUrl,
                Avatar = "🙂",
                IsDemo = false,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Users.Add(user);
        }
        else
        {
            // Refresh the profile on every sign-in — people change their name
            // and picture in Google and expect this app to notice.
            user.Email = email ?? user.Email;
            user.PictureUrl = pictureUrl ?? user.PictureUrl;
            if (!string.IsNullOrWhiteSpace(name)) user.Name = name;
        }

        user.LastLoginAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (user.Id == 0)
        {
            // Lost the race against a concurrent first sign-in from the same
            // Google account; the unique index did its job. The winner's row is
            // the one that counts, so adopt it.
            _db.Entry(user).State = EntityState.Detached;
            user = await _db.Users
                .Include(u => u.SellerProfile)
                .FirstAsync(u => u.GoogleSubjectId == googleSubjectId);
        }

        return user;
    }

    /// The claims we put in our own cookie. Deliberately small: a user id, a
    /// display name and a picture. Anything else — whether they're a cook,
    /// what their balance is — is read from the database on demand, because a
    /// claim baked into a cookie goes stale the moment the user changes and
    /// then lies until they sign out.
    public static ClaimsPrincipal BuildPrincipal(User user, string authenticationScheme)
    {
        var claims = new List<Claim>
        {
            new(AppUserIdClaim, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
        };

        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        if (!string.IsNullOrEmpty(user.PictureUrl)) claims.Add(new Claim("picture", user.PictureUrl));
        if (user.IsDemo) claims.Add(new Claim("batch:demo", "true"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationScheme));
    }

    /// The app's user id out of a signed-in principal, or null if nobody is
    /// signed in. Returns null rather than throwing: most pages are happy to
    /// render for an anonymous visitor.
    public static int? GetUserId(ClaimsPrincipal? principal)
    {
        var raw = principal?.FindFirst(AppUserIdClaim)?.Value;
        return int.TryParse(raw, out var id) ? id : null;
    }

    public async Task<User?> LoadAsync(int userId) =>
        await _db.Users
            .AsNoTracking()
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

    /// The seeded accounts offered on the sign-in page.
    public async Task<List<User>> GetDemoUsersAsync() =>
        await _db.Users
            .AsNoTracking()
            .Include(u => u.SellerProfile)
            .Where(u => u.IsDemo)
            .OrderBy(u => u.SellerProfile == null) // cooks first — they show more of the app
            .ThenBy(u => u.Name)
            .ToListAsync();

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "";

    private static string? LocalPart(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Split('@')[0];
}
