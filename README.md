# Batch (.NET 9 / Blazor Server / EF Core / SQLite)

A hyperlocal homemade-food marketplace for Australia: nearby home cooks post scheduled
"food drops" (dish, price, portions, order deadline, pickup window), buyers reserve and
pay, cooks confirm pickup with a code. Pickup-only, no delivery network. Fake login (pick
a demo user, no real auth) — built as a portfolio piece aligned to common AU .NET job
requirements (ASP.NET Core, REST Controllers, EF Core, Razor/Blazor, relational DB).

## Run it

```
./run.sh
```

Then open http://localhost:5289. On first run it creates `Marketplace.Web/marketplace.db`
(SQLite) and seeds 6 demo cooks across Sydney suburbs (Strathfield, Chatswood, Burwood,
Parramatta, Rhodes, Zetland) with realistic food drops, plus a few buyer-only demo users.
Switch "who you are" from the dropdown in the sidebar to simulate different buyers/sellers.

## Structure

- `Models/` — `FoodDrop`, `SellerProfile`, `PickupLocation`, `Order`, `Payment`, `Review`,
  `User`, `Category`, `Message`, `Favorite`
- `Data/` — `AppDbContext` (EF Core) + `DbInitializer` (seed data)
- `Services/` — business logic (`FoodDropService`, `OrderService`, `MessageService`,
  `FavoriteService`, `CurrentUserService`) plus `IPaymentGateway` / `MockPaymentGateway`
  (swappable for a real Stripe Connect integration later), shared by both the UI and the API
- `Controllers/` — REST API: `/api/categories` (Web API surface, extendable)
- `Components/Pages/` — Blazor Server UI: `Home` (discover), `FoodDropDetail`, `Checkout`,
  `OrderDetail`, `MyOrders`, `MyFoodDrops` (seller dashboard), `PostFoodDrop`, `Favorites`,
  `Messages`
- `Components/Shared/MealCard.razor` — reusable food-drop card used across Discover/Favorites

## Notes

- No real photo upload — food drops show an emoji placeholder, or paste an image URL.
- "Login" is a fake per-browser selection stored in localStorage, not real auth.
- Payment is mocked (`MockPaymentGateway`) — no real money moves.
- Not yet done (by design, for this pass): SQL Server/Postgres (currently SQLite), automated
  tests, CI/CD, real Azure deployment, real auth, seller onboarding/compliance UI, a
  dedicated seller profile page, review submission.
- To reset all data, stop the app and delete `Marketplace.Web/marketplace.db`.
