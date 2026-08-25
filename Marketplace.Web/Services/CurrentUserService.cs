using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

// Scoped per Blazor circuit (one browser tab). Not real auth — a demo-only
// "pick who you are" flow, persisted to localStorage so a refresh doesn't log you out.
public class CurrentUserService
{
    private readonly ProtectedLocalStorage _storage;
    private readonly AppDbContext _db;
    private const string StorageKey = "marketplace.currentUserId";
    private const string ModeKey = "marketplace.mode";

    public User? CurrentUser { get; private set; }

    /// Which half of the marketplace the nav is currently showing. A cook who
    /// is also a buyer switches between them; a buyer-only user is always in Eat.
    public AppMode Mode { get; private set; } = AppMode.Eat;

    /// True once the user has a SellerProfile — the single gate between
    /// "browse the Cook side" and "actually run a kitchen".
    public bool IsSeller => CurrentUser?.SellerProfile is not null;

    public event Action? OnChange;

    public CurrentUserService(ProtectedLocalStorage storage, AppDbContext db)
    {
        _storage = storage;
        _db = db;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var result = await _storage.GetAsync<int>(StorageKey);
            if (result.Success && result.Value != 0)
            {
                CurrentUser = await LoadUserAsync(result.Value);
            }

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

    public async Task LoginAsync(int userId)
    {
        CurrentUser = await LoadUserAsync(userId);
        if (CurrentUser is not null)
        {
            await _storage.SetAsync(StorageKey, userId);
        }
        // Switching to a buyer-only demo user must drop you out of Cook mode,
        // or the nav would offer a kitchen that isn't theirs.
        if (!IsSeller) Mode = AppMode.Eat;
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

    public async Task LogoutAsync()
    {
        CurrentUser = null;
        Mode = AppMode.Eat;
        await _storage.DeleteAsync(StorageKey);
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
