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

        var mayaProfile = MakeSeller(maya, "Strathfield", "Thai home cook",
            "I grew up in Chiang Mai and cook the dishes my family made at home.", VerificationStatus.Verified,
            4.9m, 43, 18);
        var yukiProfile = MakeSeller(yuki, "Chatswood", "Japanese home cook",
            "Ten years in Osaka kitchens before I moved to Sydney — I miss home-style katei ryōri, so I make it myself.", VerificationStatus.Verified,
            4.8m, 61, 27);
        var minhProfile = MakeSeller(minh, "Burwood", "Vietnamese home cook",
            "My mother's phở recipe, simmered for 12 hours the way she taught me in Hà Nội.", VerificationStatus.Verified,
            5.0m, 34, 15);
        var raviProfile = MakeSeller(ravi, "Parramatta", "Indian home cook",
            "Punjabi comfort food — the kind you'd get invited over for, not order from a menu.", VerificationStatus.Verified,
            4.7m, 52, 21);
        var soojinProfile = MakeSeller(soojin, "Rhodes", "Korean home cook",
            "I ferment my own kimchi in small batches — it's the difference you can taste.", VerificationStatus.Pending,
            null, 0, 0); // new cook, pending verification — no orders yet
        var amirProfile = MakeSeller(amir, "Zetland", "Lebanese home cook",
            "Family recipes from Tripoli, made fresh the morning of pickup, never the night before.", VerificationStatus.Verified,
            4.9m, 38, 19);

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
    }
}
