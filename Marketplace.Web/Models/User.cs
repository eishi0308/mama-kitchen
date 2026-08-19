namespace Marketplace.Web.Models;

// Simulated user — no real password/auth, this is a local demo.
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "🙂"; // emoji avatar, keeps the demo image-free

    // Present only for users who are also cooks. A user can be buyer-only,
    // seller-only, or both — this is why it's a nullable nav, not a flag.
    public SellerProfile? SellerProfile { get; set; }
}
