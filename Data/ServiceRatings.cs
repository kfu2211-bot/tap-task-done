namespace tts.Data;
public sealed record ServiceRatingItem(long Id, long TaskId, string TaskTitle, string Category, string OtherName, int Rating, string Feedback, DateTime CreatedAt, string Direction, string ViewerRole);
public sealed record ServiceRatingsView(IReadOnlyList<ServiceRatingItem> Items, bool Participant, bool Given = false);
public static class ServiceRatingRules
{
 public static IQueryable<MarketplaceReview> VisibleTo(IQueryable<MarketplaceReview> source, int userId) => source.Where(r => userId > 0 && (r.ClientId == userId || r.FreelancerId == userId));
 public static string ViewerRole(bool byFreelancer, bool given) => (given ? byFreelancer : !byFreelancer) ? "freelancer" : "client";
 public static bool CanSwitchRoles(string? workRole) => workRole == "both";
 public static string ResolveRole(string? workRole, string? requested, string? current) => workRole == "freelancer" ? "freelancer" : workRole != "both" ? "client" : requested is "client" or "freelancer" ? requested : current == "freelancer" ? "freelancer" : "client";
}
