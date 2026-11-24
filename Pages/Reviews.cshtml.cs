using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tts.Data;
namespace tts.Pages;
[Authorize]
public class ReviewsModel(ApplicationDbContext db) : PageModel
{
 public List<ServiceRatingItem> Items { get; private set; } = [];
 public string View { get; private set; } = "received";
 public string RoleFilter { get; private set; } = "client";
 public bool CanSwitchRoles { get; private set; }
 public string ContextTitle => View == "given" ? RoleFilter == "client" ? "Ratings you gave to freelancers" : "Ratings you gave to clients" : RoleFilter == "client" ? "Ratings from freelancers" : "Ratings from clients";
 public string ContextHelp => !CanSwitchRoles ? RoleFilter == "client" ? "Feedback between you and the freelancers you worked with." : "Feedback between you and the clients you worked with." : RoleFilter == "client" ? "Your feedback as a client. Freelancer work ratings stay in the freelancer view." : "Your feedback as a freelancer. Client activity ratings stay in the client view.";
 public int ReceivedCount { get; private set; }
 public int GivenCount { get; private set; }
 public long? FocusTaskId { get; private set; }
 public string? FocusTaskTitle { get; private set; }
 public bool FocusIsClient { get; private set; }
 public bool CanRate { get; private set; }
 public bool Unavailable { get; private set; }
 [TempData] public string? Notice { get; set; }
 public async Task<IActionResult> OnGetAsync(string? view, int? legacy, CancellationToken ct, string? asRole = null, long? taskId = null)
 {
  if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0) return Challenge();
  View = view == "given" ? "given" : "received";
  Response.Headers.CacheControl = "no-store";
  if (legacy == 1) return LocalRedirect(WorkspaceNavigation.WithoutLegacy(Request));
  try {
   if (!await db.Users.AnyAsync(u => u.Id == userId, ct)) return Challenge();
   var workRole = await db.FreelancerProfiles.AsNoTracking().Where(p=>p.UserId == userId).Select(p=>p.WorkRole).SingleOrDefaultAsync(ct) ?? "client";
   CanSwitchRoles = ServiceRatingRules.CanSwitchRoles(workRole);
   RoleFilter = ServiceRatingRules.ResolveRole(workRole,asRole,Request.Cookies["tts-home-view-"+userId]);
   if (taskId.HasValue) {
    var task = await db.MarketplaceTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == taskId && t.Status == "completed" && (t.ClientId == userId || db.MarketplaceOffers.Any(o => o.TaskId == t.Id && o.FreelancerId == userId && o.Status == "accepted")), ct);
    if (task is null) return NotFound();
    FocusTaskId = task.Id; FocusTaskTitle = task.Title; FocusIsClient = task.ClientId == userId;
    var taskRole = FocusIsClient ? "client" : "freelancer";
    if (ServiceRatingRules.ResolveRole(workRole,taskRole,null) != taskRole) return NotFound();
    RoleFilter = taskRole;
    CanRate = await db.MarketplaceOffers.AnyAsync(o => o.TaskId == task.Id && o.Status == "accepted",ct) && !await db.MarketplaceReviews.AnyAsync(r => r.TaskId == task.Id && (FocusIsClient ? !r.ByFreelancer && r.ClientId == userId : r.ByFreelancer && r.FreelancerId == userId), ct);
   }
   if (CanSwitchRoles && Request.Cookies["tts-home-view-"+userId] != RoleFilter)
       Response.Cookies.Append("tts-home-view-"+userId,RoleFilter,new CookieOptions { HttpOnly=true,Secure=Request.IsHttps,SameSite=SameSiteMode.Lax,MaxAge=TimeSpan.FromDays(90) });
   var rows = await (from r in ServiceRatingRules.VisibleTo(db.MarketplaceReviews.AsNoTracking(),userId)
    join task in db.MarketplaceTasks.AsNoTracking() on r.TaskId equals task.Id
    join client in db.Users.AsNoTracking() on r.ClientId equals client.Id
    join freelancer in db.Users.AsNoTracking() on r.FreelancerId equals freelancer.Id
    where !taskId.HasValue || r.TaskId == taskId
    orderby r.CreatedAt descending, r.Id descending
    select new { Review = r, task.Title, task.Category, ClientName = (client.FirstName + " " + client.LastName).Trim(), FreelancerName = (freelancer.FirstName + " " + freelancer.LastName).Trim() }).ToListAsync(ct);
   var mapped = rows.Select(x => {
    var given = (x.Review.ByFreelancer ? x.Review.FreelancerId : x.Review.ClientId) == userId;
    var role = ServiceRatingRules.ViewerRole(x.Review.ByFreelancer, given);
    var name = (given ? x.Review.ByFreelancer : !x.Review.ByFreelancer) ? x.ClientName : x.FreelancerName;
    return new { Given = given, Item = new ServiceRatingItem(x.Review.Id,x.Review.TaskId,x.Title,x.Category,name,Math.Clamp(x.Review.Rating,1,5),x.Review.Feedback,x.Review.CreatedAt,x.Review.ByFreelancer ? "Freelancer → Client" : "Client → Freelancer",role) };
   }).Where(x => x.Item.ViewerRole == RoleFilter).ToList();
   ReceivedCount = mapped.Count(x => !x.Given); GivenCount = mapped.Count(x => x.Given);
   Items = mapped.Where(x => x.Given == (View == "given")).Select(x => x.Item).ToList();
  } catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException) { Unavailable = true; }
  return Page();
 }
}
