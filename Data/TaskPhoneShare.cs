using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class TaskPhoneShare
{
    public long ConversationId { get; set; }
    public int UserId { get; set; }

    public static bool IsActive(string status) => status is
        "open" or "booking-pending" or "assigned" or "on-the-way" or "arrived" or "in-progress" or "awaiting-confirmation";

    public static IQueryable<UserContact> SharedContacts(ApplicationDbContext db, long conversationId) =>
        from share in db.Set<TaskPhoneShare>().AsNoTracking()
        join contact in db.Set<UserContact>().AsNoTracking() on share.UserId equals contact.UserId
        where share.ConversationId == conversationId && contact.PhoneNumber != ""
        select contact;
}
