namespace tts.Data;

public static class TaskTracking
{
    // Participant authorization remains on the caller's accepted-offer query.
    public static IQueryable<MarketplaceTask> VisibleTasks(IQueryable<MarketplaceTask> tasks, long requestedTask = 0) =>
        tasks.Where(task => task.Status == "assigned" || task.Status == "on-the-way" || task.Status == "arrived" ||
            task.Status == "in-progress" || task.Status == "awaiting-confirmation" || task.Status == "disputed" ||
            (task.Id == requestedTask && (task.Status == "completed" || task.Status == "cancelled")));
}
