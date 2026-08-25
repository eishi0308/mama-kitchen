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
    // Appended (never inserted mid-enum): these are persisted as ints, so
    // reordering the members above would silently re-label every stored order.
    BuyerCancelled,
}

// Which "world" the UI is currently serving. A single person can be both a
// buyer and a cook, so this is a view mode, not a role on the user record.
public enum AppMode
{
    Eat,
    Cook,
}

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    Refunded,
}

// Money going out to a cook, as opposed to PaymentStatus which is money coming
// in from a buyer. Separate lifecycles, so separate enums.
public enum PayoutStatus
{
    Pending,
    Paid,
    Failed,
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
