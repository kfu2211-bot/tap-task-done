namespace tts.Data;

public static class JobActivity
{
    public static bool IsClosedTask(string status) => status is "cancelled" or "canceled" or "expired" or "moderated";
    public static bool IsClosed(MarketplaceTask task, MarketplaceOffer offer) =>
        IsClosedTask(task.Status) || offer.Status is "cancelled" or "canceled" or "withdrawn";
    public static bool MatchesClosed(MarketplaceTask task, MarketplaceOffer offer, string filter) => filter switch {
        "cancelled" => task.Status is "cancelled" or "canceled" || offer.Status is "cancelled" or "canceled",
        "expired" => task.Status == "expired",
        "withdrawn" => offer.Status == "withdrawn",
        _ => true
    };
    public static bool InView(MarketplaceTask task, MarketplaceOffer offer, string view, bool pendingOnly, DateTime now) => view switch {
        "closed" => IsClosed(task, offer),
        "offers" => !IsClosed(task, offer) && task.Status != "completed" && offer.Status != "accepted" &&
            (!pendingOnly || offer.Status == "pending" && task.Status == "open" && (task.PostExpiresUtc == null || task.PostExpiresUtc > now)),
        "scheduled" => offer.Status == "accepted" && TaskSchedule.IsUpcoming(task, now),
        "active" => offer.Status == "accepted" && !IsClosed(task, offer) &&
            task.Status is "assigned" or "on-the-way" or "arrived" or "in-progress" or "awaiting-confirmation" or "disputed" && !TaskSchedule.IsUpcoming(task, now),
        "completed" => offer.Status == "accepted" && task.Status == "completed",
        _ => false
    };
}
