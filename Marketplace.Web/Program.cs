using Marketplace.Web.Auth;
using Marketplace.Web.Components;
using Marketplace.Web.Data;
using Marketplace.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Database (SQLite file next to the project) ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=marketplace.db"));

// --- App services (shared by Blazor pages and the REST API) ---
builder.Services.AddScoped<IFoodDropService, FoodDropService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<ISellerService, SellerService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<UserAccountService>();

// Mocked Stripe Connect-shaped payment gateway — see IPaymentGateway for the
// swap-in path to a real provider.
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();

// --- Authentication -------------------------------------------------------
// Cookie is the app's own scheme (it holds our user id and nothing else);
// Google is only ever used to establish who someone is, once, at sign-in.
// There is no ASP.NET Core Identity here on purpose: Google-only means no
// passwords, no lockout, no confirmation emails, no two-factor — Identity's
// entire surface would be dead weight wrapped around one integer.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "batch.auth";
        options.Cookie.HttpOnly = true;             // JS can never read it
        options.Cookie.SameSite = SameSiteMode.Lax; // survives the OAuth redirect back from Google
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;

        // Cookie auth answers "not signed in" with a 302 to the login page,
        // which is right for a browser and useless for an API client: curl and
        // fetch() both see 200 OK and a page of HTML instead of 401. Under
        // /api the status code is the answer.
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

// Registered only when credentials are present, so the app still runs — and
// the demo accounts still work — on a machine that has never been given a
// Google client id. Without this guard, a missing secret is a startup crash.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;

        // Must match the "Authorised redirect URI" registered in the Google
        // Cloud console exactly, including scheme, port and trailing path.
        options.CallbackPath = "/signin-google";

        options.SaveTokens = false; // we never call a Google API on the user's behalf
        // Google's default claim mappings don't include the avatar, and the
        // sign-in page wants one.
        options.ClaimActions.MapJsonKey("picture", "picture");

        options.Events.OnTicketReceived = AuthEndpoints.OnGoogleTicketReceived;
    });
}

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// --- Blazor + REST API ---
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // EF Core's relationship fix-up links Category <-> Listings <-> Favorites back and
    // forth in memory; without this, System.Text.Json walks that cycle forever.
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

// Create the SQLite DB (if missing) and seed demo data on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Seed(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Order matters: authentication populates HttpContext.User, authorization then
// enforces against it, and antiforgery needs the identity to bind tokens to.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthEndpoints(); // /auth/login/google, /auth/demo, /auth/logout
app.MapControllers(); // REST API: /api/listings, /api/messages, etc.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
