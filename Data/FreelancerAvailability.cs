using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public static class FreelancerAvailability
{
    public static readonly string[] BlockingStates = ["on-the-way", "arrived", "in-progress"];
    public static string Effective(string preference, IEnumerable<string> states)
    {
        var active = states.ToHashSet();
        if (active.Contains("in-progress")) return "Working";
        if (active.Contains("arrived")) return "Busy · Arrived";
        if (active.Contains("on-the-way")) return "Busy · On the way";
        return preference;
    }
    public static IQueryable<MarketplaceTask> ActiveTasks(ApplicationDbContext db, int userId) =>
        db.MarketplaceTasks.Where(t => BlockingStates.Contains(t.Status) && db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == userId && o.Status == "accepted"));
    public static Task<bool> IsBusyAsync(ApplicationDbContext db, int userId, CancellationToken ct) => ActiveTasks(db, userId).AnyAsync(ct);
    public static async Task<string> GetAsync(ApplicationDbContext db, int userId, string preference, CancellationToken ct) =>
        Effective(preference, await ActiveTasks(db, userId).AsNoTracking().Select(t => t.Status).ToListAsync(ct));
    public static async Task<Dictionary<int, string[]>> AssignmentsAsync(ApplicationDbContext db, int[] ids, CancellationToken ct)
    {
        var rows = await (from o in db.MarketplaceOffers.AsNoTracking()
            join t in db.MarketplaceTasks.AsNoTracking() on o.TaskId equals t.Id
            where ids.Contains(o.FreelancerId) && o.Status == "accepted" && BlockingStates.Contains(t.Status)
            select new { o.FreelancerId, t.Status }).ToListAsync(ct);
        return rows.GroupBy(x => x.FreelancerId).ToDictionary(g => g.Key, g => g.Select(x => x.Status).ToArray());
    }
}
