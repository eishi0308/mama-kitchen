using Marketplace.Web.Components;
using Marketplace.Web.Data;
using Marketplace.Web.Services;
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
builder.Services.AddScoped<CurrentUserService>();

// Mocked Stripe Connect-shaped payment gateway — see IPaymentGateway for the
// swap-in path to a real provider.
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();

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
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers(); // REST API: /api/listings, /api/messages, etc.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
