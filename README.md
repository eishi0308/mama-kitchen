# Batch (.NET 9 / Blazor Server / EF Core / SQLite)

A hyperlocal homemade-food marketplace for Australia: nearby home cooks post scheduled
"food drops" (dish, price, portions, order deadline, pickup window), buyers reserve and
pay, cooks confirm pickup with a code. Pickup-only, no delivery network. Real sign-in
through Google (OAuth 2.0 + cookie auth), plus one-click demo accounts so the app can be
explored without one — built as a portfolio piece aligned to common AU .NET job
requirements (ASP.NET Core, REST Controllers, EF Core, Razor/Blazor, relational DB, auth).

It is a **two-sided** marketplace, and both sides are built end to end: any demo user can
become a cook, run a kitchen, and sell — and any cook is also a buyer.

## Run it

```
./run.sh
```

Then open http://localhost:5289 and pick a demo account on the sign-in screen — Google
sign-in needs credentials first, see **Signing in** below. On first run it creates
`Marketplace.Web/marketplace.db`
(SQLite) and seeds 6 demo cooks across Sydney suburbs (Strathfield, Chatswood, Burwood,
Parramatta, Rhodes, Zetland), a few buyer-only demo users, current food drops, and about
six months of trading history — past batches, collected orders, no-shows and reviews.
Every cook's rating, order count and earnings figure is **derived from those rows**, not
hard-coded, so they stay correct as you use the app.

While signed into a demo account you can switch between the seeded people from the sidebar.
Everyone gets an **Eat / Cook** switch at the top of the sidebar — the two halves of the
marketplace — and the sidebar follows whichever page you're on.

## Signing in

Two ways in, and they are not equal.

**Google** is the real one. First sign-in creates the account — there is no separate
signup form, because Google already told us the name, email and picture. No password is
ever created or stored; the only credential this app holds is Google's opaque `sub`
claim. Accounts are matched on that subject id and never on email address, so a recycled
Google address can't inherit the previous owner's orders and payouts.

**Demo accounts** are the seeded people (Maya, Priya, …), flagged `IsDemo` in the
database and offered as one-click buttons on `/login`. They exist so the two-sided story
can be explored — including "See the other side" — without anyone having to hand over a
Google account. `/auth/demo` refuses any row without that flag, so the demo door can
never open a real person's account.

### Enabling Google sign-in

The app runs fine without this — you'll see a notice on `/login` and the demo accounts
still work. To turn it on:

1. In the [Google Cloud console](https://console.cloud.google.com/apis/credentials),
   create (or pick) a project.
2. **OAuth consent screen** → External → fill in app name and support email. While the app
   is in *Testing*, add your own Google address under **Test users** or sign-in will be
   refused.
3. **Credentials → Create credentials → OAuth client ID → Web application**.
4. Under **Authorised redirect URIs** add exactly:

   ```
   http://localhost:5289/signin-google
   ```

   This must match character for character — scheme, port and path. It is
   `/signin-google`, not `/auth/callback`; that's the Google handler's `CallbackPath`.
   Add your production URL there too when you deploy.
5. Put the client id and secret into user-secrets (never `appsettings.json`, which is
   committed):

   ```
   dotnet user-secrets --project Marketplace.Web set "Authentication:Google:ClientId" "<your id>"
   dotnet user-secrets --project Marketplace.Web set "Authentication:Google:ClientSecret" "<your secret>"
   ```

6. Restart. The **Continue with Google** button appears on its own.

In production supply the same two values as environment variables
(`Authentication__Google__ClientId`) or from a secret store, and serve over HTTPS so the
auth cookie can be marked `Secure`.

### How it's wired

| Piece | Where |
|---|---|
| Cookie + Google schemes, `OnRedirectToLogin` | `Program.cs` |
| `/auth/login/google`, `/auth/demo`, `/auth/logout` | `Auth/AuthEndpoints.cs` |
| Google ticket → `User` row, claims | `Services/UserAccountService.cs` |
| Reading "who am I" for the UI | `Services/CurrentUserService.cs` |
| `User.AppUserId()` for controllers | `Auth/ClaimsPrincipalExtensions.cs` |

Sign-in and sign-out are plain HTTP endpoints rather than Blazor event handlers on
purpose: writing an auth cookie writes a response header, and an interactive Blazor Server
component runs over a SignalR circuit whose headers went out long ago. Every change of
identity is a real navigation.

There is no ASP.NET Core Identity here. Google-only means no passwords, no lockout, no
confirmation emails and no 2FA — Identity's whole surface would be scaffolding around one
integer.

## Seeing the flow

Open **`/how-it-works`** (or "How it works" in the sidebar) for the whole thing on one
page: both journeys step by step, where the money goes, and what happens to your address,
your bank details and allergens.

Inside the app, every state answers *"what do I do now?"* with one instruction — the
buyer's order page and the cook's batch page both lead with it.

And on any order page there's a **"See the other side"** card that switches you to the
other person and lands you on the matching page. That's the fastest way to understand a
two-sided marketplace: reserve a meal as Priya, jump to Maya to cook it and take the
pickup code, then jump back and watch the same order ask you to rate it.

## The two journeys

**Buyer** — Discover (search, cuisine and dietary filters, tonight/tomorrow buckets) →
food drop detail → cook's public profile (story, verification, reviews, their other
batches) → checkout → order tracking with a live status track and a 4-digit pickup code →
free cancellation with refund until orders close → rate the meal across four dimensions →
order again.

**Cook** — Start selling (one screen: your story, and where people collect) → add a payout
account → post a batch with a live preview of the buyer's card → kitchen dashboard (what to
cook today, what to hand over, balance) → per-batch management: drive it through *taking
orders → cooking → ready → done*, confirm each pickup by code, refund or mark a no-show →
**cash out to your bank** → read and reply to reviews → cook the same batch again in two
clicks.

Advancing a batch cascades onto every live order, so the buyer's tracker moves the moment
the cook taps a button.

### The money, end to end

All three legs run through `IPaymentGateway`, which is mocked:

| Leg | Trigger | Record |
|---|---|---|
| **In** — buyer is charged | Checkout | `Payment` (Succeeded) |
| **Back** — buyer is refunded | Buyer cancels before deadline, or cook cancels | `Payment` → Refunded |
| **Out** — cook is paid | Cook cashes out their balance | `Payout`, and every order it covers is stamped |

A cook's balance is the net of collected orders **not yet attached to a payout** — not a
time-based estimate — so the balance and the payout history can never disagree, and the
same money can't be paid out twice. No full bank account number is ever stored: the form
takes it, derives the last four digits, and discards the rest.

## Structure

- `Models/` — `FoodDrop`, `SellerProfile`, `PickupLocation`, `Order`, `Payment`, `Payout`,
  `Review`, `User`, `Category`, `Message`, `Favorite`
- `Data/` — `AppDbContext` (EF Core) + `DbInitializer` (demo data + trading history)
- `Auth/` — `AuthEndpoints` (sign in / out), `ClaimsPrincipalExtensions` (`User.AppUserId()`)
- `Services/` — business logic shared by the UI and the API: `FoodDropService`,
  `OrderService`, `SellerService`, `MessageService`, `FavoriteService`,
  `UserAccountService` (Google identity → `User` row), `CurrentUserService` (who am I,
  for the UI), plus `IPaymentGateway` / `MockPaymentGateway` (charge **and refund**,
  swappable for real Stripe Connect later)
- `Controllers/` — REST API: `/api/fooddrops`, `/api/orders`, `/api/cooks`,
  `/api/categories`, `/api/messages`, `/api/favorites`, `/api/users/me`. Browsing is
  anonymous; everything that acts as somebody requires the auth cookie and reads the
  acting user from it.
- `Components/Pages/` — buyer: `Home`, `FoodDropDetail`, `CookProfile`, `Checkout`,
  `OrderDetail`, `MyOrders`, `Favorites`, `Messages`; cook: `SellerOnboarding`, `Kitchen`,
  `KitchenDrop`, `PostFoodDrop` (create + edit + repeat), `KitchenEarnings`,
  `KitchenReviews`, `KitchenProfile`; plus `Login`
- `Components/Shared/` — `MealCard`, `Icon`, `StarRating`, `OrderStatusTrack`, `StatTile`,
  `DemoSignInForm`, `RedirectToLogin`

## Routes

| Buyer | | Cook | |
|---|---|---|---|
| `/` | Discover | `/sell/start` | Become a cook |
| `/food/{id}` | Food drop | `/kitchen` | Dashboard |
| `/checkout/{id}` | Checkout | `/kitchen/drops/{id}` | Manage one batch |
| `/orders` | My orders | `/kitchen/drops/{id}/edit` | Edit a batch |
| `/orders/{id}` | Order tracking | `/post` | New food drop |
| `/cooks/{id}` | Cook's public page | `/post?from={id}` | Repeat a past batch |
| `/favorites` | Saved | `/kitchen/earnings` | Earnings + cash out |
| `/messages` | Shared by both sides | `/kitchen/reviews` | Reviews + replies |
| `/how-it-works` | Both journeys on one page | | |
| `/login` | Sign in (Google or demo) | `/kitchen/profile` | Profile, payout account, pickup points |

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
- A cook can't be paid without a payout account, can't cash out below $10, and the same
  earnings can never be paid out twice.
- Only the cook who sold a meal can reply to its review.
- The acting user always comes from the auth cookie, never from a request parameter — so
  no request can act as, or read the private data of, anyone but its sender.
- `/auth/demo` will only ever sign you into a row flagged `IsDemo`.
- `returnUrl` is validated to a same-site path, so sign-in can't be used as an open redirect.

## Notes

- No real photo upload — food drops show an emoji placeholder, or paste an image URL.
- Sign-in is real: Google OAuth 2.0 (PKCE, `openid profile email`) with an HttpOnly cookie
  session. The demo accounts are a deliberate, flagged shortcut for exploring the app, not
  a bypass — see **Signing in**.
- Payment is mocked (`MockPaymentGateway`) — no real money moves, in either direction. The
  platform fee is 10% (`SellerService.PlatformFeeRate`) and the minimum payout is $10.
- Seeded current drops are generated *relative to when you first run the app*, so if you
  first run it at 2am the pickup windows are early-morning. Delete the db and rerun during
  the day for evening windows.
- The schema is created with `EnsureCreated()`, not EF migrations, so a model change means
  deleting `marketplace.db` and letting it reseed. Startup says so if the file is stale.
- Not yet done (by design, for this pass): SQL Server/Postgres (currently SQLite), EF
  migrations, automated tests, CI/CD, real Azure deployment, photo upload, email/push
  notifications, and the `Disputed` order state (modelled, no UI).
- To reset all data, stop the app and delete `Marketplace.Web/marketplace.db`.
