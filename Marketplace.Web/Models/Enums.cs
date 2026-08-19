namespace Marketplace.Web.Models;

// A food drop's lifecycle. Kept as an explicit state machine (not a pile of
// booleans) so "can this be ordered right now" is one property, not a guess.
public enum FoodDropStatus
{
    Draft,
    Published,
    OrderingClosed,
    Preparing,
    Ready,
    Completed,
    Cancelled,
}

// An individual order's lifecycle. PendingPayment -> Confirmed is the
// mocked-payment handshake; everything after that mirrors the real pickup flow.
public enum OrderStatus
{
    PendingPayment,
    Confirmed,
    Preparing,
    Ready,
    Collected,
    BuyerNoShow,
    SellerCancelled,
    Refunded,
    Disputed,
}

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    Refunded,
}

// Australian food-business compliance is council/state/food-type dependent —
// this app can't determine legality, only track where a seller is in the process.
public enum VerificationStatus
{
    NotStarted,
    Pending,
    Verified,
    ActionRequired,
    Expired,
}

// Dietary labels as flags so a dish can be several at once (Vegetarian + GlutenFree, etc).
[Flags]
public enum DietaryLabel
{
    None = 0,
    Vegetarian = 1,
    Vegan = 2,
    Halal = 4,
    GlutenFree = 8,
    DairyFree = 16,
    HighProtein = 32,
}
