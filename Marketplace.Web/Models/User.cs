namespace Marketplace.Web.Models;

// A person. Either a real account signed in through Google, or one of the
// seeded demo accounts anyone can step into to explore the app.
//
// There is no password column and there never will be: Google is the only
// real identity provider, so the only credential this app ever holds is an
// opaque subject id issued by Google. Nothing here is worth stealing.
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "🙂"; // emoji avatar, keeps the demo image-free

    /// Google's `sub` claim — stable, opaque, and the only safe join key.
    /// Deliberately NOT the email address: Google accounts can change their
    /// primary email, and matching on email is how you accidentally hand one
    /// person's order history to another. Null for demo accounts, which have
    /// no Google identity at all.
    public string? GoogleSubjectId { get; set; }

    /// Shown back to the person so they can tell which account they're in.
    /// Never used to look them up — see GoogleSubjectId.
    public string? Email { get; set; }

    /// Google's profile picture URL. Optional: the UI falls back to the emoji
    /// avatar, which is what every seeded account uses.
    public string? PictureUrl { get; set; }

    /// True for the seeded accounts on the sign-in page. Gates the demo
    /// sign-in endpoint — without this flag that endpoint would be a way to
    /// become *any* user in the database, including real ones.
    public bool IsDemo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Present only for users who are also cooks. A user can be buyer-only,
    // seller-only, or both — this is why it's a nullable nav, not a flag.
    public SellerProfile? SellerProfile { get; set; }
}
