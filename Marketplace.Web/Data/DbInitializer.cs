using Marketplace.Web.Models;

namespace Marketplace.Web.Data;

// Seeds realistic Sydney-suburb demo data — cooks, cuisines, and a spread of
// food drops covering the "tonight / tomorrow / this weekend" buckets plus
// the sold-out and low-stock states, so the UI never looks empty or fake.
public static class DbInitializer
{
    public static void Seed(AppDbContext db)
    {
        db.Database.EnsureCreated();

        if (db.Users.Any()) return; // already seeded

        // --- Buyers (no SellerProfile — pure buyer-side demo accounts) ---
        var eishi = new User { Name = "Eishi", Avatar = "🧑‍💻" };
        var haruka = new User { Name = "Haruka", Avatar = "👩‍🎨" };
        var tom = new User { Name = "Tom", Avatar = "🧔" };
        var priya = new User { Name = "Priya", Avatar = "👩‍🔬" };
        db.Users.AddRange(eishi, haruka, tom, priya);

        // --- Cooks ---
        var maya = new User { Name = "Maya", Avatar = "👩‍🍳" };
        var yuki = new User { Name = "Yuki", Avatar = "👨‍🍳" };
        var minh = new User { Name = "Minh", Avatar = "👩‍🍳" };
        var ravi = new User { Name = "Ravi", Avatar = "👨‍🍳" };
        var soojin = new User { Name = "Soo-jin", Avatar = "👩‍🍳" };
        var amir = new User { Name = "Amir", Avatar = "👨‍🍳" };
        db.Users.AddRange(maya, yuki, minh, ravi, soojin, amir);
        db.SaveChanges();

        var categories = new[]
        {
            new Category { Name = "Thai", Icon = "🌶️" },
            new Category { Name = "Japanese", Icon = "🍱" },
            new Category { Name = "Vietnamese", Icon = "🍜" },
            new Category { Name = "Indian", Icon = "🍛" },
            new Category { Name = "Korean", Icon = "🥘" },
            new Category { Name = "Lebanese", Icon = "🥙" },
        };
        db.Categories.AddRange(categories);
        db.SaveChanges();
        var thai = categories[0]; var japanese = categories[1]; var vietnamese = categories[2];
        var indian = categories[3]; var korean = categories[4]; var lebanese = categories[5];

        SellerProfile MakeSeller(
            User user, string suburb, string cuisine, string story, VerificationStatus status,
            decimal? rating, int completedOrders, int repeatCustomers) => new()
        {
            UserId = user.Id,
            Suburb = suburb,
            Cuisine = cuisine,
            Story = story,
            VerificationStatus = status,
            JoinedAt = DateTime.UtcNow.AddMonths(-Random.Shared.Next(2, 10)),
            RatingAverage = rating,
            CompletedOrders = completedOrders,
            RepeatCustomers = repeatCustomers,
        };

        // Rating / order counts are all passed as null+0 here and then DERIVED
        // from the seeded order + review history at the bottom of this file.
        // Hand-written stats drift the moment anyone uses the app.
        var mayaProfile = MakeSeller(maya, "Strathfield", "Thai home cook",
            "I grew up in Chiang Mai and cook the dishes my family made at home.", VerificationStatus.Verified,
            null, 0, 0);
        var yukiProfile = MakeSeller(yuki, "Chatswood", "Japanese home cook",
            "Ten years in Osaka kitchens before I moved to Sydney — I miss home-style katei ryōri, so I make it myself.", VerificationStatus.Verified,
            null, 0, 0);
        var minhProfile = MakeSeller(minh, "Burwood", "Vietnamese home cook",
            "My mother's phở recipe, simmered for 12 hours the way she taught me in Hà Nội.", VerificationStatus.Verified,
            null, 0, 0);
        var raviProfile = MakeSeller(ravi, "Parramatta", "Indian home cook",
            "Punjabi comfort food — the kind you'd get invited over for, not order from a menu.", VerificationStatus.Verified,
            null, 0, 0);
        var soojinProfile = MakeSeller(soojin, "Rhodes", "Korean home cook",
            "I ferment my own kimchi in small batches — it's the difference you can taste.", VerificationStatus.Pending,
            null, 0, 0); // new cook, pending verification — no orders yet
        var amirProfile = MakeSeller(amir, "Zetland", "Lebanese home cook",
            "Family recipes from Tripoli, made fresh the morning of pickup, never the night before.", VerificationStatus.Verified,
            null, 0, 0);

        db.SellerProfiles.AddRange(mayaProfile, yukiProfile, minhProfile, raviProfile, soojinProfile, amirProfile);
        db.SaveChanges();

        PickupLocation MakeLocation(SellerProfile seller, string suburb, double km, string label, string address, string instructions) => new()
        {
            SellerProfileId = seller.Id,
            Suburb = suburb,
            ApproxDistanceKm = km,
            Label = label,
            ExactAddress = address,
            Instructions = instructions,
        };

        var mayaLoc = MakeLocation(mayaProfile, "Strathfield", 1.1, "Front gate", "14 Beresford Rd, Strathfield NSW 2135", "Blue letterbox out front — I'll leave it in an insulated bag if I'm mid-cook.");
        var yukiLoc = MakeLocation(yukiProfile, "Chatswood", 0.85, "Building lobby", "22 Anderson St, Chatswood NSW 2067", "Lobby table near the mailboxes, unit 12. Text me if you can't find it.");
        var minhLoc = MakeLocation(minhProfile, "Burwood", 1.6, "Front porch", "8 Wentworth Rd, Burwood NSW 2134", "Esky on the porch — help yourself, no need to knock.");
        var raviLoc = MakeLocation(raviProfile, "Parramatta", 2.3, "Apartment entrance", "5/40 Marsden St, Parramatta NSW 2150", "Ground floor entrance, buzz 5 if the door's locked.");
        var soojinLoc = MakeLocation(soojinProfile, "Rhodes", 1.4, "Reception", "3 Shoreline Dr, Rhodes NSW 2138", "Building reception desk, ask for Soo-jin's pickup.");
        var amirLoc = MakeLocation(amirProfile, "Zetland", 2.8, "Community pickup point", "Zetland Ave shops, outside Cafe 21", "I'll be out front 10 minutes either side of the window with a labelled esky.");

        db.PickupLocations.AddRange(mayaLoc, yukiLoc, minhLoc, raviLoc, soojinLoc, amirLoc);
        db.SaveChanges();

        var now = DateTime.UtcNow;

        FoodDrop Drop(
            string title, string desc, string emoji, decimal price, int total, int remaining,
            TimeSpan closesIn, TimeSpan pickupStartsIn, TimeSpan pickupLasts,
            Category category, SellerProfile seller, PickupLocation location,
            string ingredients, string allergens, DietaryLabel dietary = DietaryLabel.None) => new()
        {
            Title = title,
            Description = desc,
            ImageEmoji = emoji,
            Price = price,
            PortionsTotal = total,
            PortionsRemaining = remaining,
            OrderDeadline = now + closesIn,
            PickupWindowStart = now + pickupStartsIn,
            PickupWindowEnd = now + pickupStartsIn + pickupLasts,
            CategoryId = category.Id,
            SellerId = seller.UserId,
            PickupLocationId = location.Id,
            Ingredients = ingredients,
            Allergens = allergens,
            Dietary = dietary,
            Status = FoodDropStatus.Published,
        };

        var drops = new[]
        {
            // Tonight
            Drop("Thai Green Curry", "Slow-simmered green curry with chicken thigh, Thai eggplant and basil. Served with jasmine rice.", "🍛", 12,
                15, 6, TimeSpan.FromHours(3), TimeSpan.FromHours(4), TimeSpan.FromHours(1.5), thai, mayaProfile, mayaLoc,
                "Chicken thigh, coconut milk, green curry paste, Thai eggplant, basil, jasmine rice", "Contains shellfish paste, may contain peanuts", DietaryLabel.GlutenFree),
            Drop("Japanese Chicken Curry", "Katsu-style curry — crumbed chicken, mild curry roux, steamed rice, pickled daikon.", "🍱", 12,
                10, 3, TimeSpan.FromHours(2.5), TimeSpan.FromHours(3.5), TimeSpan.FromHours(1.5), japanese, yukiProfile, yukiLoc,
                "Chicken breast, panko, curry roux, steamed rice, daikon pickle", "Contains gluten, egg"),
            Drop("Salmon Teriyaki Bento", "Pan-seared salmon, teriyaki glaze, rice, seasonal vegetables.", "🍣", 14,
                8, 0, TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(1), japanese, yukiProfile, yukiLoc,
                "Salmon, soy, mirin, rice, seasonal vegetables", "Contains fish, soy, gluten"),
            Drop("Bún Chả (Grilled Pork Vermicelli)", "Charcoal-grilled pork patties and belly, rice vermicelli, herbs, nước chấm.", "🍜", 13,
                14, 9, TimeSpan.FromHours(4), TimeSpan.FromHours(5), TimeSpan.FromHours(2), vietnamese, minhProfile, minhLoc,
                "Pork, rice vermicelli, lettuce, herbs, nước chấm dipping sauce", "Contains fish sauce, peanuts"),
            Drop("Butter Chicken + Rice", "Tomato-based butter chicken, basmati rice, garlic naan on the side.", "🍗", 13,
                20, 2, TimeSpan.FromMinutes(90), TimeSpan.FromHours(2.5), TimeSpan.FromHours(1.5), indian, raviProfile, raviLoc,
                "Chicken thigh, tomato, cream, butter, basmati rice, naan", "Contains dairy, gluten"),
            Drop("Kimchi Jjigae", "Bubbling kimchi stew with pork belly, tofu and a fried egg on rice.", "🍲", 12,
                10, 4, TimeSpan.FromHours(3.5), TimeSpan.FromHours(4.5), TimeSpan.FromHours(1.5), korean, soojinProfile, soojinLoc,
                "Kimchi, pork belly, tofu, egg, rice", "Contains soy, egg", DietaryLabel.GlutenFree),
            Drop("Chicken Shawarma Plate", "Marinated chicken thigh, garlic toum, pickles, rice, fresh salad.", "🥙", 14,
                16, 8, TimeSpan.FromHours(3), TimeSpan.FromHours(4), TimeSpan.FromHours(2), lebanese, amirProfile, amirLoc,
                "Chicken thigh, garlic toum, pickles, rice, salad", "Contains dairy"),

            // Tomorrow
            Drop("Pad Kra Pao (Basil Chicken)", "Stir-fried minced chicken with holy basil and chilli, fried egg, jasmine rice.", "🌶️", 13,
                12, 12, TimeSpan.FromHours(27), TimeSpan.FromHours(28), TimeSpan.FromHours(1.5), thai, mayaProfile, mayaLoc,
                "Minced chicken, holy basil, chilli, garlic, egg, jasmine rice", "Contains egg, fish sauce", DietaryLabel.GlutenFree),
            Drop("Phở Bò (Beef Pho)", "12-hour beef bone broth, rice noodles, rare beef, herbs.", "🍜", 14,
                10, 10, TimeSpan.FromHours(26), TimeSpan.FromHours(27.5), TimeSpan.FromHours(2), vietnamese, minhProfile, minhLoc,
                "Beef bones, beef slices, rice noodles, herbs, bean sprouts", "May contain gluten (check broth)", DietaryLabel.DairyFree),
            Drop("Bibimbap Bowl", "Mixed rice bowl with seasoned vegetables, bulgogi beef, gochujang, fried egg.", "🥘", 13,
                12, 12, TimeSpan.FromHours(29), TimeSpan.FromHours(30), TimeSpan.FromHours(1.5), korean, soojinProfile, soojinLoc,
                "Beef bulgogi, rice, seasoned vegetables, gochujang, egg", "Contains soy, egg, sesame"),

            // This weekend
            Drop("Chana Masala (Vegetarian)", "Slow-cooked chickpea curry, cumin rice, side of pickle.", "🍛", 11,
                15, 15, TimeSpan.FromHours(78), TimeSpan.FromHours(80), TimeSpan.FromHours(2), indian, raviProfile, raviLoc,
                "Chickpeas, tomato, cumin, basmati rice, pickle", "None declared", DietaryLabel.Vegetarian | DietaryLabel.Vegan | DietaryLabel.GlutenFree | DietaryLabel.DairyFree),
            Drop("Mixed Mezze Platter (Vegetarian)", "Hummus, baba ghanoush, tabbouleh, falafel, warm pita.", "🥙", 15,
                10, 10, TimeSpan.FromHours(90), TimeSpan.FromHours(92), TimeSpan.FromHours(2.5), lebanese, amirProfile, amirLoc,
                "Chickpeas, tahini, eggplant, parsley, bulgur, pita", "Contains sesame, gluten", DietaryLabel.Vegetarian),
        };

        db.FoodDrops.AddRange(drops);
        db.SaveChanges();

        SeedHistory(db, categories, new[] { eishi, haruka, tom, priya },
            new[] { mayaProfile, yukiProfile, minhProfile, raviProfile, amirProfile },
            new[] { mayaLoc, yukiLoc, minhLoc, raviLoc, amirLoc });

        // Eishi is the "you" account the demo opens on, so give them a live
        // order against a drop that's still happening tonight. Without this the
        // buyer's order tracker, pickup code and the cook's handover queue are
        // all empty on first run and read as broken rather than new.
        SeedLiveOrder(db, buyer: eishi, drop: drops[0]);

        foreach (var profile in new[] { mayaProfile, yukiProfile, minhProfile, raviProfile, soojinProfile, amirProfile })
        {
            RecomputeStats(db, profile);
        }

        // Soo-jin is deliberately left without payout details: she's the new cook,
        // and she demonstrates the "you can't be paid yet" state honestly.
        SeedPayoutsAndReplies(db, new[] { mayaProfile, yukiProfile, minhProfile, raviProfile, amirProfile });
    }

    // Past payouts and a few cook replies, so the earnings and reviews screens
    // open with real history instead of a zeroed-out empty state.
    private static void SeedPayoutsAndReplies(AppDbContext db, SellerProfile[] cooks)
    {
        var replies = new[]
        {
            "Thanks so much — the kids helped fold these, so I'll pass that on.",
            "Really glad you enjoyed it. There's another batch going up next week.",
            "Thank you for coming out in that weather. See you next time.",
        };

        var apologies = new[]
        {
            "Sorry about the wait — I had two batches overlap and got behind. I've cut the pickup window down so it can't happen again.",
            "That's fair, the portion was smaller than usual that night. I've put the serve size back up and you're welcome to a bigger one next time.",
            "Thanks for saying so honestly. The chilli was dialled back for a big order and I should have noted that on the listing.",
        };

        var quietCount = 0;
        var warmCount = 0;

        foreach (var cook in cooks)
        {
            cook.PayoutAccountName = db.Users.First(u => u.Id == cook.UserId).Name;
            cook.PayoutBsb = "062000";
            // Only the last four digits are ever stored — see SellerProfile.
            cook.PayoutAccountLast4 = Rng.Next(1000, 10000).ToString();
            cook.PayoutReference = $"mock_acct_{Guid.NewGuid():N}"[..22];
            cook.PayoutSetupAt = cook.JoinedAt.AddDays(1);

            // Everything collected more than five days ago has already been paid
            // out; the rest stays in the balance so "cash out" is live on open.
            var cutoff = DateTime.UtcNow.AddDays(-5);
            var settled = db.Orders
                .Where(o => o.FoodDrop!.SellerId == cook.UserId
                            && (o.Status == OrderStatus.Collected || o.Status == OrderStatus.BuyerNoShow)
                            && (o.CollectedAt ?? o.CreatedAt) < cutoff)
                .ToList();

            if (settled.Count > 0)
            {
                var gross = settled.Sum(o => o.TotalAmount);
                var fee = Math.Round(gross * 0.10m, 2, MidpointRounding.AwayFromZero);
                var payout = new Payout
                {
                    SellerUserId = cook.UserId,
                    Amount = gross - fee,
                    GrossAmount = gross,
                    FeeAmount = fee,
                    OrderCount = settled.Count,
                    Status = PayoutStatus.Paid,
                    Reference = $"mock_tr_{Guid.NewGuid():N}"[..24],
                    Destination = $"Bank ••••{cook.PayoutAccountLast4}",
                    CreatedAt = cutoff,
                    PaidAt = cutoff,
                };
                db.Payouts.Add(payout);
                db.SaveChanges();

                foreach (var order in settled) order.PayoutId = payout.Id;
                db.SaveChanges();
            }

            // A couple of replies each, biased towards the reviews that actually
            // need one — a cook answering a 3-star is the point of the feature.
            var cookReviews = db.Reviews
                .Where(r => r.Order!.FoodDrop!.SellerId == cook.UserId)
                .OrderBy(r => r.Id)
                .ToList();

            foreach (var review in cookReviews)
            {
                var overall = (review.FoodQuality + review.Value + review.Accuracy + review.PickupExperience) / 4m;
                if (overall <= 3.5m && quietCount < 4)
                {
                    review.SellerResponse = apologies[quietCount % apologies.Length];
                    review.SellerRespondedAt = review.CreatedAt.AddHours(Rng.Next(2, 20));
                    quietCount++;
                }
                else if (overall >= 4.5m && warmCount < 6 && Rng.Next(0, 4) == 0)
                {
                    review.SellerResponse = replies[warmCount % replies.Length];
                    review.SellerRespondedAt = review.CreatedAt.AddHours(Rng.Next(2, 30));
                    warmCount++;
                }
            }
            db.SaveChanges();
        }
    }

    // A fixed seed keeps the demo reproducible: the same cooks get the same
    // ratings and the same review text on every fresh database.
    private static readonly Random Rng = new(20260826);

    private record PastDish(string Title, string Desc, string Emoji, decimal Price, string Ingredients, string Allergens);

    // Builds the transaction history the marketplace needs to look alive: past
    // batches, collected orders, payments and reviews. Every rating a buyer
    // reads on a cook's page traces back to a row created here.
    private static void SeedHistory(
        AppDbContext db, Category[] categories, User[] buyers, SellerProfile[] cooks, PickupLocation[] locations)
    {
        var menus = new Dictionary<int, (Category Cuisine, PastDish[] Dishes)>
        {
            [cooks[0].Id] = (categories[0], new[]
            {
                new PastDish("Massaman Beef Curry", "Slow-braised beef cheek, potato, peanuts, roti on the side.", "🍛", 14, "Beef cheek, coconut milk, massaman paste, potato, peanuts", "Contains peanuts"),
                new PastDish("Som Tam & Sticky Rice", "Green papaya salad pounded to order, with grilled chicken and sticky rice.", "🥗", 12, "Green papaya, lime, chilli, peanuts, chicken, sticky rice", "Contains peanuts, fish sauce"),
            }),
            [cooks[1].Id] = (categories[1], new[]
            {
                new PastDish("Oyakodon", "Chicken and egg simmered in dashi over rice — the Osaka way.", "🍚", 12, "Chicken thigh, egg, dashi, onion, rice", "Contains egg, soy, gluten"),
                new PastDish("Tonkotsu Ramen Kit", "18-hour pork broth, chashu, ajitama — assemble at home in 4 minutes.", "🍜", 16, "Pork bones, chashu, noodles, marinated egg, spring onion", "Contains gluten, egg, soy"),
            }),
            [cooks[2].Id] = (categories[2], new[]
            {
                new PastDish("Bánh Mì Thịt Nướng", "Grilled pork, pâté, pickled carrot and daikon in a crackling baguette.", "🥖", 11, "Pork, pâté, pickled vegetables, coriander, baguette", "Contains gluten, egg"),
                new PastDish("Cơm Tấm Sườn", "Broken rice with lemongrass grilled pork chop, egg cake and nước chấm.", "🍖", 13, "Pork chop, broken rice, egg, nước chấm", "Contains egg, fish sauce"),
            }),
            [cooks[3].Id] = (categories[3], new[]
            {
                new PastDish("Rogan Josh", "Kashmiri lamb curry, slow-cooked with yoghurt and fennel.", "🍲", 15, "Lamb shoulder, yoghurt, Kashmiri chilli, fennel, rice", "Contains dairy"),
                new PastDish("Dal Makhani + Naan", "Black lentils simmered overnight with butter and cream.", "🍛", 11, "Black lentils, kidney beans, butter, cream, naan", "Contains dairy, gluten"),
            }),
            [cooks[4].Id] = (categories[5], new[]
            {
                new PastDish("Lamb Kofta Plate", "Charcoal kofta, garlic sauce, pickles, rice and salad.", "🍢", 14, "Lamb mince, garlic toum, pickles, rice, salad", "Contains dairy"),
                new PastDish("Fatteh Bil Hummus", "Crisp pita, chickpeas, warm yoghurt, toasted pine nuts.", "🥘", 12, "Chickpeas, yoghurt, pita, pine nuts, garlic", "Contains dairy, gluten, nuts"),
            }),
        };

        var comments = new[]
        {
            "Genuinely better than the restaurant version down the road. Portion was huge too.",
            "Second time ordering from here and it's been perfect both times.",
            "Pickup was easy and the food was still hot when I got home. Will order again.",
            "Really generous portion for the price. My partner asked me to order it again next week.",
            "You can taste that this is someone's actual family recipe, not a commercial kitchen.",
            "Lovely to chat to at pickup, and the food was excellent.",
            "Exactly as described, ready right on time. No notes.",
            "Great flavour. I'd have liked a touch more chilli but that's personal preference.",
            "Reheated beautifully the next day, which is the real test.",
            "Best thing I've eaten this month and it was 5 minutes from my flat.",
        };

        var quietComments = new[]
        {
            "Good food, pickup ran about ten minutes late.",
            "Tasty, though a smaller portion than I expected.",
            "Solid, would order again if the timing suited.",
        };

        // Per-cook reputation bias. Without it every cook converges on the same
        // average and the Discover grid gives a buyer nothing to choose between.
        // Index matches the `cooks` array: Maya, Yuki, Minh, Ravi, Amir.
        var cookBias = new[] { 0.55, 0.35, 0.85, 0.10, 0.45 };
        var biasByDrop = new Dictionary<int, double>();

        var allDrops = new List<FoodDrop>();
        var allOrders = new List<Order>();
        var allPayments = new List<Payment>();
        var allReviews = new List<Review>();

        for (var c = 0; c < cooks.Length; c++)
        {
            var cook = cooks[c];
            var location = locations[c];
            var (cuisine, dishes) = menus[cook.Id];

            // Six past batches per cook, walked backwards a few days at a time,
            // so the earnings page has a believable trailing history rather than
            // one lump of orders on a single date.
            for (var batch = 0; batch < 6; batch++)
            {
                var dish = dishes[batch % dishes.Length];
                var daysAgo = 3 + (batch * 5) + Rng.Next(0, 3);
                var pickupStart = DateTime.UtcNow.AddDays(-daysAgo).Date.AddHours(18.5);
                var portions = Rng.Next(8, 15);

                var drop = new FoodDrop
                {
                    Title = dish.Title,
                    Description = dish.Desc,
                    ImageEmoji = dish.Emoji,
                    Price = dish.Price,
                    PortionsTotal = portions,
                    PortionsRemaining = 0,
                    OrderDeadline = pickupStart.AddHours(-2),
                    PickupWindowStart = pickupStart,
                    PickupWindowEnd = pickupStart.AddHours(1.5),
                    CategoryId = cuisine.Id,
                    SellerId = cook.UserId,
                    PickupLocationId = location.Id,
                    Ingredients = dish.Ingredients,
                    Allergens = dish.Allergens,
                    Status = FoodDropStatus.Completed,
                    CreatedAt = pickupStart.AddDays(-2),
                };
                db.FoodDrops.Add(drop);
                db.SaveChanges();
                allDrops.Add(drop);
                biasByDrop[drop.Id] = cookBias[c];

                // Most of the batch sells; a couple of portions going unsold is
                // normal and keeps the numbers from looking manufactured.
                var soldPortions = portions - Rng.Next(0, 3);
                var remaining = soldPortions;

                while (remaining > 0)
                {
                    var qty = Math.Min(remaining, Rng.Next(1, 3));
                    remaining -= qty;

                    var buyer = buyers[Rng.Next(buyers.Length)];
                    var confirmedAt = drop.OrderDeadline.AddHours(-Rng.Next(1, 20));

                    // One order in roughly fifteen is a no-show — it stays
                    // unrefunded on purpose (the cook made the food), which is
                    // what makes the earnings page's numbers non-trivial.
                    var noShow = Rng.Next(0, 15) == 0;

                    var order = new Order
                    {
                        FoodDropId = drop.Id,
                        BuyerId = buyer.Id,
                        Quantity = qty,
                        UnitPriceSnapshot = drop.Price,
                        TotalAmount = drop.Price * qty,
                        Status = noShow ? OrderStatus.BuyerNoShow : OrderStatus.Collected,
                        PickupCode = Rng.Next(1000, 10000).ToString(),
                        CreatedAt = confirmedAt,
                        ConfirmedAt = confirmedAt,
                        CollectedAt = noShow ? null : drop.PickupWindowStart.AddMinutes(Rng.Next(5, 85)),
                        CancelledAt = noShow ? drop.PickupWindowEnd : null,
                        CancellationReason = noShow ? "Buyer did not collect within the pickup window" : null,
                    };
                    allOrders.Add(order);
                }
            }
        }

        db.Orders.AddRange(allOrders);
        db.SaveChanges();

        foreach (var order in allOrders)
        {
            allPayments.Add(new Payment
            {
                OrderId = order.Id,
                Provider = "MockConnect",
                Status = PaymentStatus.Succeeded,
                Amount = order.TotalAmount,
                Reference = $"mock_pi_{Guid.NewGuid():N}"[..24],
                CreatedAt = order.CreatedAt,
                ProcessedAt = order.ConfirmedAt,
            });

            // Only a collected order can be reviewed, and only about 70% of
            // buyers actually leave one — mirroring the real completion rate
            // rather than pretending every order gets rated.
            if (order.Status != OrderStatus.Collected || Rng.Next(0, 10) >= 7) continue;

            // A stronger cook draws fewer lukewarm reviews and more fives.
            var bias = biasByDrop.GetValueOrDefault(order.FoodDropId, 0.4);
            var quiet = Rng.NextDouble() > 0.10 + bias;
            int Score() => quiet
                ? Rng.Next(3, 5)
                : (Rng.NextDouble() < bias ? 5 : Rng.Next(4, 6));

            allReviews.Add(new Review
            {
                OrderId = order.Id,
                FoodQuality = Score(),
                Value = Score(),
                Accuracy = Score(),
                PickupExperience = Score(),
                Comment = quiet ? quietComments[Rng.Next(quietComments.Length)] : comments[Rng.Next(comments.Length)],
                CreatedAt = (order.CollectedAt ?? order.CreatedAt).AddHours(Rng.Next(2, 40)),
            });
        }

        db.Payments.AddRange(allPayments);
        db.Reviews.AddRange(allReviews);
        db.SaveChanges();

        // Leave the demo's own account one collected order with no review, so
        // the "rate your meal" flow is reachable the moment you open the app.
        var eishiCollected = allOrders
            .Where(o => o.BuyerId == buyers[0].Id && o.Status == OrderStatus.Collected)
            .OrderByDescending(o => o.CollectedAt)
            .FirstOrDefault();

        if (eishiCollected is not null)
        {
            var review = db.Reviews.FirstOrDefault(r => r.OrderId == eishiCollected.Id);
            if (review is not null)
            {
                db.Reviews.Remove(review);
                db.SaveChanges();
            }
        }
    }

    // Puts one paid, confirmed order on a drop that's still open, so both sides
    // of the handover flow (buyer's pickup code, cook's handover queue) have
    // something real in them on a fresh database.
    private static void SeedLiveOrder(AppDbContext db, User buyer, FoodDrop drop)
    {
        var confirmedAt = DateTime.UtcNow.AddMinutes(-40);
        var order = new Order
        {
            FoodDropId = drop.Id,
            BuyerId = buyer.Id,
            Quantity = 1,
            UnitPriceSnapshot = drop.Price,
            TotalAmount = drop.Price,
            Status = OrderStatus.Confirmed,
            PickupCode = Rng.Next(1000, 10000).ToString(),
            CreatedAt = confirmedAt,
            ConfirmedAt = confirmedAt,
        };
        db.Orders.Add(order);

        // The reserved portion has to come out of the batch, or the drop would
        // advertise a portion that is already spoken for.
        drop.PortionsRemaining = Math.Max(0, drop.PortionsRemaining - 1);
        db.SaveChanges();

        db.Payments.Add(new Payment
        {
            OrderId = order.Id,
            Provider = "MockConnect",
            Status = PaymentStatus.Succeeded,
            Amount = order.TotalAmount,
            Reference = $"mock_pi_{Guid.NewGuid():N}"[..24],
            CreatedAt = confirmedAt,
            ProcessedAt = confirmedAt,
        });
        db.SaveChanges();
    }

    // Same derivation SellerService.RecomputeStatsAsync performs at runtime.
    // Duplicated here rather than shared because the seeder runs against a raw
    // DbContext at startup, before the DI scope that owns the services exists.
    private static void RecomputeStats(AppDbContext db, SellerProfile profile)
    {
        var scores = db.Reviews
            .Where(r => r.Order!.FoodDrop!.SellerId == profile.UserId)
            .Select(r => (r.FoodQuality + r.Value + r.Accuracy + r.PickupExperience) / 4m)
            .ToList();

        profile.RatingAverage = scores.Count == 0 ? null : Math.Round(scores.Average(), 1);

        var collected = db.Orders
            .Where(o => o.FoodDrop!.SellerId == profile.UserId && o.Status == OrderStatus.Collected)
            .Select(o => o.BuyerId)
            .ToList();

        profile.CompletedOrders = collected.Count;
        profile.RepeatCustomers = collected.GroupBy(id => id).Count(g => g.Count() > 1);
        db.SaveChanges();
    }
}
