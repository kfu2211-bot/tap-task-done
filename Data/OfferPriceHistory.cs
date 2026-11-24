using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace tts.Data;
public record OfferPriceChange(long OfferId, decimal Previous, decimal Current);
public static class OfferPriceHistory
{
    public const string Event = "offer-price-updated";
    public static TaskProgressEvent Record(MarketplaceOffer offer, decimal previous) => new() { TaskId = offer.TaskId, ActorId = offer.FreelancerId,
        Status = Event, CreatedAtUtc = DateTime.UtcNow, Note = JsonSerializer.Serialize(new OfferPriceChange(offer.Id, previous, offer.Price)) };
    public static OfferPriceChange? Parse(string? note)
    {
        try { var value = JsonSerializer.Deserialize<OfferPriceChange>(note ?? ""); return value is { OfferId: > 0, Previous: > 0, Current: > 0 } ? value : null; }
        catch (JsonException) { return null; }
    }
    public static async Task<Dictionary<long, OfferPriceChange>> LoadAsync(ApplicationDbContext db, IEnumerable<MarketplaceOffer> offers, CancellationToken ct)
    {
        var list = offers.ToList(); var tasks = list.Select(o => o.TaskId).Distinct().ToArray();
        if (tasks.Length == 0) return [];
        var rows = await db.Set<TaskProgressEvent>().AsNoTracking().Where(e => tasks.Contains(e.TaskId) && e.Status == Event).OrderByDescending(e => e.Id)
            .Select(e => new { e.TaskId, e.ActorId, e.Note }).ToListAsync(ct);
        return rows.Select(e => new { Row = e, Change = Parse(e.Note) }).Where(e => e.Change is not null && list.Any(o =>
            o.Id == e.Change.OfferId && o.TaskId == e.Row.TaskId && o.FreelancerId == e.Row.ActorId && o.Price == e.Change.Current))
            .GroupBy(e => e.Change!.OfferId).ToDictionary(g => g.Key, g => g.First().Change!);
    }
}
