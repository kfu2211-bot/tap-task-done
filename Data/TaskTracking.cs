namespace tts.Data;

public static class TaskTracking
{
    public static bool ShowPicker(bool history, int count) => !history && count > 1;
    public static bool FullHistory(MarketplaceTask task, MarketplaceOffer? offer, int viewer) =>
        viewer > 0 && task.Status is "completed" or "cancelled" && offer?.Status == "accepted" && (task.ClientId == viewer || offer.FreelancerId == viewer);
    // Participant authorization remains on the caller's accepted-offer query.
    public static IQueryable<MarketplaceTask> VisibleTasks(IQueryable<MarketplaceTask> tasks, long requestedTask = 0) =>
        tasks.Where(task => task.Status == "assigned" || task.Status == "on-the-way" || task.Status == "arrived" ||
            task.Status == "in-progress" || task.Status == "awaiting-confirmation" || task.Status == "disputed" ||
            (task.Id == requestedTask && (task.Status == "completed" || task.Status == "cancelled")));
}
