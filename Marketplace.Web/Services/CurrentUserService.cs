using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

// Scoped per Blazor circuit (one browser tab).
//
// Who you are now comes from the auth cookie, not from localStorage: this reads
// the ClaimsPrincipal, pulls our user id out of it, and loads the row. The
// public surface (CurrentUser / IsSeller / Mode) is unchanged from the demo-login
// version on purpose — every page in the app reads this service and none of them
// had to learn what a ClaimsPrincipal is.
//
// What is *not* stored in the cookie: whether you're a cook. That's read fresh
// from the database, because a claim minted at sign-in would still say "buyer"
// for the rest of the session after someone opens a kitchen.
public class CurrentUserService
{
    private readonly ProtectedLocalStorage _storage;
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _authState;
    private const string ModeKey = "marketplace.mode";

    public User? CurrentUser { get; private set; }

    /// Which half of the marketplace the nav is currently showing. A cook who
    /// is also a buyer switches between them; a buyer-only user is always in Eat.
    public AppMode Mode { get; private set; } = AppMode.Eat;

    /// True once the user has a SellerProfile — the single gate between
    /// "browse the Cook side" and "actually run a kitchen".
    public bool IsSeller => CurrentUser?.SellerProfile is not null;

    /// True when signed into one of the seeded accounts rather than a real
    /// Google account. The UI says so, so nobody mistakes the demo for theirs.
    public bool IsDemoAccount => CurrentUser?.IsDemo == true;

    public bool IsSignedIn => CurrentUser is not null;

    public event Action? OnChange;

    public CurrentUserService(
        ProtectedLocalStorage storage,
        AppDbContext db,
        AuthenticationStateProvider authState)
    {
        _storage = storage;
        _db = db;
        _authState = authState;
    }

    public async Task InitializeAsync()
    {
        var state = await _authState.GetAuthenticationStateAsync();
        var userId = UserAccountService.GetUserId(state.User);

        CurrentUser = userId is null ? null : await LoadUserAsync(userId.Value);

        // Eat/Cook is a view preference, not an identity fact, so it stays in
        // localStorage — it should survive a sign-out and it isn't worth a
        // round trip to the database or a column on Users.
        try
        {
            var mode = await _storage.GetAsync<string>(ModeKey);
            if (mode.Success && Enum.TryParse<AppMode>(mode.Value, out var parsed) && IsSeller)
            {
                Mode = parsed;
            }
        }
        catch (InvalidOperationException)
        {
            // JS interop not available yet (prerendering) — ignore, page will retry after render.
        }

        OnChange?.Invoke();
    }

    /// Re-reads the current user from the database. Called after seller
    /// onboarding so the nav flips to Cook mode without a page reload.
    public async Task RefreshAsync()
    {
        if (CurrentUser is null) return;
        CurrentUser = await LoadUserAsync(CurrentUser.Id);
        OnChange?.Invoke();
    }

    public async Task SetModeAsync(AppMode mode)
    {
        if (mode == AppMode.Cook && !IsSeller) return;
        Mode = mode;
        try
        {
            await _storage.SetAsync(ModeKey, mode.ToString());
        }
        catch (InvalidOperationException)
        {
            // Prerender — the in-memory Mode is still correct for this render.
        }
        OnChange?.Invoke();
    }

    // SellerProfile has to be eagerly loaded: IsSeller is read on every nav
    // render, and a lazy nav would either N+1 or silently report false.
    private async Task<User?> LoadUserAsync(int userId) =>
        await _db.Users
            .AsNoTracking()
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);
}
