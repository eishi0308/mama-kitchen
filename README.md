# Batch (.NET 9 / Blazor Server / EF Core / SQLite)

A hyperlocal homemade-food marketplace for Australia: nearby home cooks post scheduled
"food drops" (dish, price, portions, order deadline, pickup window), buyers reserve and
pay, cooks confirm pickup with a code. Pickup-only, no delivery network. Fake login (pick
a demo user, no real auth) — built as a portfolio piece aligned to common AU .NET job
requirements (ASP.NET Core, REST Controllers, EF Core, Razor/Blazor, relational DB).

It is a **two-sided** marketplace, and both sides are built end to end: any demo user can
become a cook, run a kitchen, and sell — and any cook is also a buyer.

## Run it

```
./run.sh
```

Then open http://localhost:5289. On first run it creates `Marketplace.Web/marketplace.db`
(SQLite) and seeds 6 demo cooks across Sydney suburbs (Strathfield, Chatswood, Burwood,
Parramatta, Rhodes, Zetland), a few buyer-only demo users, current food drops, and about
six months of trading history — past batches, collected orders, no-shows and reviews.
Every cook's rating, order count and earnings figure is **derived from those rows**, not
hard-coded, so they stay correct as you use the app.

Switch "who you are" from the dropdown in the sidebar to move between people. Cooks get an
**Eat / Cook** switch; the sidebar follows whichever page you're on.

## The two journeys

**Buyer** — Discover (search, cuisine and dietary filters, tonight/tomorrow buckets) →
food drop detail → cook's public profile (story, verification, reviews, their other
batches) → checkout → order tracking with a live status track and a 4-digit pickup code →
free cancellation with refund until orders close → rate the meal across four dimensions →
order again.

**Cook** — Start selling (one screen: your story, and where people collect) → post a batch
with a live preview of the buyer's card → kitchen dashboard (what to cook today, what to
hand over, balance) → per-batch management: drive it through *taking orders → cooking →
ready → done*, confirm each pickup by code, refund or mark a no-show → earnings with the
platform fee broken out → edit your public profile and pickup points.

Advancing a batch cascades onto every live order, so the buyer's tracker moves the moment
the cook taps a button.

## Structure

- `Models/` — `FoodDrop`, `SellerProfile`, `PickupLocation`, `Order`, `Payment`, `Review`,
  `User`, `Category`, `Message`, `Favorite`
- `Data/` — `AppDbContext` (EF Core) + `DbInitializer` (demo data + trading history)
- `Services/` — business logic shared by the UI and the API: `FoodDropService`,
  `OrderService`, `SellerService`, `MessageService`, `FavoriteService`,
  `CurrentUserService`, plus `IPaymentGateway` / `MockPaymentGateway` (charge **and
  refund**, swappable for real Stripe Connect later)
- `Controllers/` — REST API: `/api/fooddrops`, `/api/orders`, `/api/cooks`,
  `/api/categories`, `/api/messages`, `/api/favorites`, `/api/users`
- `Components/Pages/` — buyer: `Home`, `FoodDropDetail`, `CookProfile`, `Checkout`,
  `OrderDetail`, `MyOrders`, `Favorites`, `Messages`; cook: `SellerOnboarding`, `Kitchen`,
  `KitchenDrop`, `PostFoodDrop` (create + edit), `KitchenEarnings`, `KitchenProfile`
- `Components/Shared/` — `MealCard`, `Icon`, `StarRating`, `OrderStatusTrack`, `StatTile`

## Routes

| Buyer | | Cook | |
|---|---|---|---|
| `/` | Discover | `/sell/start` | Become a cook |
| `/food/{id}` | Food drop | `/kitchen` | Dashboard |
| `/checkout/{id}` | Checkout | `/kitchen/drops/{id}` | Manage one batch |
| `/orders` | My orders | `/kitchen/drops/{id}/edit` | Edit a batch |
| `/orders/{id}` | Order tracking | `/post` | New food drop |
| `/cooks/{id}` | Cook's public page | `/kitchen/earnings` | Earnings |
| `/favorites` | Saved | `/kitchen/profile` | Profile + pickup points |
| `/messages` | Shared by both sides | | |

## Rules the app actually enforces

- Portions are reserved with an atomic conditional update, so a batch can't oversell.
- Order totals are always computed server-side, and each order snapshots its unit price —
  a cook raising the price never re-charges an existing buyer.
- Exact pickup addresses are only ever revealed on a confirmed, paid order.
- A buyer cancels free until the order deadline; a cook can cancel any time and the buyer
  is always refunded in full. Cancelling a batch refunds every live order on it.
- A no-show is only available once the pickup window has closed, and is not refunded.
- Reviews can only be left on a collected order, once, and recompute the cook's rating.
- A batch can't be edited below the portions buyers already reserved.

## Notes

- No real photo upload — food drops show an emoji placeholder, or paste an image URL.
- "Login" is a fake per-browser selection stored in localStorage, not real auth. The API
  therefore takes a user id as a query parameter where real auth would supply it.
- Payment is mocked (`MockPaymentGateway`) — no real money moves. Payouts are simulated as
  settling 48h after collection. The platform fee is 10% (`SellerService.PlatformFeeRate`).
- Seeded current drops are generated *relative to when you first run the app*, so if you
  first run it at 2am the pickup windows are early-morning. Delete the db and rerun during
  the day for evening windows.
- Not yet done (by design, for this pass): SQL Server/Postgres (currently SQLite),
  automated tests, CI/CD, real Azure deployment, real auth, photo upload, push
  notifications, and the `Disputed` order state (modelled, no UI).
- To reset all data, stop the app and delete `Marketplace.Web/marketplace.db`.
