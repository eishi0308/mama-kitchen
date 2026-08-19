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

    public User? CurrentUser { get; private set; }
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
                CurrentUser = await _db.Users.FindAsync(result.Value);
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
        CurrentUser = await _db.Users.FindAsync(userId);
        if (CurrentUser is not null)
        {
            await _storage.SetAsync(StorageKey, userId);
        }
        OnChange?.Invoke();
    }

    public async Task LogoutAsync()
    {
        CurrentUser = null;
        await _storage.DeleteAsync(StorageKey);
        OnChange?.Invoke();
    }
}
