namespace Marketplace.Web.Models;

// Two-tier address model: everything here is safe to show before purchase
// EXCEPT ExactAddress/Instructions, which the UI must only reveal on a
// confirmed order. See Section 5 of the product brief — never expose a
// seller's exact home address publicly.
public class PickupLocation
{
    public int Id { get; set; }

    public int SellerProfileId { get; set; }
    public SellerProfile? SellerProfile { get; set; }

    public string Label { get; set; } = ""; // e.g. "Front gate"
    public string Suburb { get; set; } = ""; // public — e.g. "Strathfield"
    public double ApproxDistanceKm { get; set; } // demo value; a real build derives this from geocoding

    // Private — only surfaced to a buyer with a Confirmed order for this location.
    public string ExactAddress { get; set; } = "";
    public string Instructions { get; set; } = "";
}
