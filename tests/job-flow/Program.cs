using System.Text;
using tts.Data;
using Microsoft.EntityFrameworkCore;

var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    checks++;
    Console.WriteLine("PASS " + label);
}

var revisionOffer = new MarketplaceOffer { Id = 42, Status = "pending", Price = 700, Message = "Original offer" };
var originalRevision = OfferRevision.For(revisionOffer);
revisionOffer.Price = 701;
Check(OfferRevision.For(revisionOffer) != originalRevision, "A revised price invalidates stale acceptance");
revisionOffer.Price = 700;
revisionOffer.Message = "Changed scope";
Check(OfferRevision.For(revisionOffer) != originalRevision, "Changed offer terms invalidate stale acceptance");
revisionOffer.Message = "Original offer";
revisionOffer.Status = "withdrawn";
Check(OfferRevision.For(revisionOffer) != originalRevision, "Withdrawal invalidates pending offer controls");

var trackingTasks = new[] {
    new MarketplaceTask { Id = 1, Status = "assigned" },
    new MarketplaceTask { Id = 2, Status = "awaiting-confirmation" },
    new MarketplaceTask { Id = 3, Status = "completed" },
    new MarketplaceTask { Id = 4, Status = "cancelled" },
    new MarketplaceTask { Id = 5, Status = "open" }
}.AsQueryable();
var expiredTask = new MarketplaceTask { Id = 17, ClientId = 42, Status = "expired" };
var withdrawnOffer = new MarketplaceOffer { TaskId = 17, Status = "withdrawn" };
Check(JobActivity.InView(expiredTask, withdrawnOffer, "closed", false, DateTime.UtcNow), "Expired task with withdrawn offer appears in Closed");
Check(!JobActivity.InView(expiredTask, withdrawnOffer, "offers", false, DateTime.UtcNow), "Closed work does not leak into My offers");
Check(JobActivity.MatchesClosed(expiredTask, withdrawnOffer, "expired") && JobActivity.MatchesClosed(expiredTask, withdrawnOffer, "withdrawn"), "Task expiration and offer withdrawal remain independently filterable");
Check(!JobActivity.MatchesClosed(expiredTask, withdrawnOffer, "cancelled"), "Expired does not pretend to mean cancelled");
Check(JobActivity.IsClosed(new MarketplaceTask { Status = "open" }, withdrawnOffer), "Withdrawn offer is closed even while the public task stays open");
Check(JobActivity.InView(new MarketplaceTask { Status = "open" }, new MarketplaceOffer { Status = "pending" }, "offers", false, DateTime.UtcNow), "Open pending offer remains in My offers");
Check(!JobActivity.InView(new MarketplaceTask { Status = "assigned" }, new MarketplaceOffer { Status = "accepted" }, "offers", false, DateTime.UtcNow), "Accepted offer is removed from My offers");
Check(JobActivity.InView(new MarketplaceTask { Status = "assigned" }, new MarketplaceOffer { Status = "declined" }, "offers", false, DateTime.UtcNow), "Declined offers remain available in the declined view");
Check(!JobActivity.InView(new MarketplaceTask { Status = "assigned" }, new MarketplaceOffer { Status = "declined" }, "offers", true, DateTime.UtcNow), "Pending view does not include declined offers");
Check(!JobActivity.InView(new MarketplaceTask { Status = "moderated" }, new MarketplaceOffer { Status = "accepted" }, "active", false, DateTime.UtcNow), "Removed work cannot appear as active");
Check(JobActivity.InView(new MarketplaceTask { Status = "completed" }, new MarketplaceOffer { Status = "accepted" }, "completed", false, DateTime.UtcNow), "Completed accepted work retains its own tab");
Check(TaskReuse.CanReuse(expiredTask, 42) && !TaskReuse.CanReuse(expiredTask, 43), "Only the original client can reuse an expired task");
Check(!TaskReuse.CanReuse(new MarketplaceTask { ClientId = 42, Status = "in-progress" }, 42), "Active work cannot be recreated through closed-record actions");
var provenance = TaskReuse.SourceEvent(99, expiredTask.Id, 42);
Check(provenance.TaskId == 99 && provenance.Note == "17" && provenance.ActorId == 42 && expiredTask.Status == "expired", "New task keeps a source reference without changing original history");
foreach (var closedStatus in new[] { "completed", "cancelled", "canceled", "expired", "moderated" })
{
    Check(tts.Pages.MessagesModel.IsArchived(closedStatus), $"{closedStatus} conversations belong in Archived");
    Check(!tts.Pages.MessagesModel.MaySend(closedStatus, true, true, true), $"{closedStatus} blocks messaging even with an accepted offer or pending booking");
}
Check(!tts.Pages.MessagesModel.IsArchived("awaiting-confirmation") && tts.Pages.MessagesModel.MaySend("awaiting-confirmation", true, false), "Work awaiting client confirmation keeps its conversation active");
Check(!tts.Pages.MessagesModel.IsArchived("inquiry") && tts.Pages.MessagesModel.MaySend("inquiry", false, false, true), "Private service inquiries remain active");
Check(TaskTracking.VisibleTasks(trackingTasks).Select(t => t.Id).SequenceEqual(new long[] { 1, 2 }), "Tracking excludes completed/cancelled/open tasks and retains work awaiting client confirmation");
Check(TaskTracking.VisibleTasks(trackingTasks, 3).Select(t => t.Id).SequenceEqual(new long[] { 1, 2, 3 }), "Explicit history request includes only the requested completed task alongside active candidates");
Check(!TaskTracking.VisibleTasks(trackingTasks, 999).Any(t => t.Status == "completed"), "Unknown history ID does not expose unrelated completed tasks");

Check(TaskProgress.ValidCompletion("Work completed.", false), "Completion note alone is accepted");
Check(TaskProgress.ValidCompletion(null, true), "Video alone is accepted");
Check(TaskProgress.ValidCompletion("Finished", true), "Note and video together are accepted");
Check(TaskProgress.ValidCompletion("   ", false), "Completion note and video are optional");
Check(!TaskProgress.ValidCompletion(new string('a', 1001), true), "Oversized completion note is rejected even with video");

var mp4 = new byte[32];
Encoding.ASCII.GetBytes("ftyp").CopyTo(mp4, 4);
Encoding.ASCII.GetBytes("isom").CopyTo(mp4, 8);
Check(CompletionMedia.VideoType(mp4) == "video/mp4", "MP4 signature is recognized");
var webm = new byte[32];
new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }.CopyTo(webm, 0);
Encoding.ASCII.GetBytes("webm").CopyTo(webm, 8);
Check(CompletionMedia.VideoType(webm) == "video/webm", "WebM signature is recognized");
Check(CompletionMedia.VideoType([0xff, 0xd8, 0xff, 0xe0]) is null, "Photo cannot be uploaded as completion video");
Check(CompletionMedia.VideoType(new byte[CompletionMedia.MaxBytes + 1]) is null, "Oversized completion video is rejected");
Check(CompletionMedia.VideoType(new byte[32]) is null, "Unrecognized upload is rejected");

var plumbing = new MarketplaceTask { Category = "Repairs", SpecificService = "Plumbing" };
const string specialties = """{"Repairs":["Plumbing","Other: Small repairs"]}""";
Check(ServiceCatalog.Matches("Repairs", specialties, plumbing), "Matching specialization is included");
Check(!ServiceCatalog.Matches("Repairs", """{"Repairs":["Electrical repair"]}""", plumbing), "Other specialization does not match Plumbing");
Check(!ServiceCatalog.Matches("Repairs", null, plumbing), "Legacy category-only profile is not claimed to offer Plumbing");
Check(ServiceCatalog.Matches("Repairs", null, new MarketplaceTask { Category = "Repairs" }), "Legacy general task matches category");
Check(!ServiceCatalog.Matches("Cleaning", specialties, plumbing), "Different category is excluded");
Check(!ServiceCatalog.Matches(null, null, plumbing), "Empty profile requires All jobs");
Check(ServiceCatalog.Matches("Repairs", specialties, new MarketplaceTask { Category = "Repairs", SpecificService = "Other" }), "Custom category specialization is included");

Check(JobDuration.Valid(null), "Historical jobs can omit duration");
Check(JobDuration.Valid(120) && JobDuration.Display(120) == "2 hour(s)", "Two-hour estimate is preserved");
Check(!JobDuration.Valid(0) && !JobDuration.Valid(-1) && !JobDuration.Valid(43201), "Invalid duration values are rejected");
Check(AgreementRules.Editable("assigned") && AgreementRules.Editable("in-progress"), "Schedule can be revised before and during work");
Check(!AgreementRules.Editable("completed") && !AgreementRules.Editable("cancelled"), "Closed jobs cannot change agreement");
Check(!AgreementRules.SameTerms(new WorkAgreement { EstimatedMinutes = 60 }, new WorkAgreement { EstimatedMinutes = 120 }), "Duration change creates new terms");
Check(!AgreementRules.CanProceed(new WorkAgreement { ClientConfirmedAtUtc = DateTime.UtcNow }), "One-party confirmation cannot proceed");
Check(AgreementRules.CanProceed(new WorkAgreement { ClientConfirmedAtUtc = DateTime.UtcNow, FreelancerConfirmedAtUtc = DateTime.UtcNow }), "Both confirmations permit progress");
Check(JobCancellation.CanRequest("in-progress") && JobCancellation.CanRequest("on-the-way"), "Travelling and working jobs allow cancellation request");
Check(!JobCancellation.CanRequest("completed") && JobCancellation.CanRequest("assigned"), "Accepted jobs can request cancellation before travel; completed jobs remain closed");
Check(!TaskProgress.CanAdvance("completed", "finish", false, true, true), "Completed job cannot be finished again");
Check(!TaskProgress.CanAdvance("in-progress", "confirm", true, false, true), "Client confirmation requires submitted completion");
Check(JobTypes.Valid("short") && JobTypes.Valid("long") && JobTypes.Valid("multi-day"), "Only the three supported job types are accepted");
Check(!JobTypes.Valid(null) && !JobTypes.Valid("custom"), "Missing or invented job types are rejected on new work");
Check(JobTypes.Label(null) == "Not classified", "Historical work is not inferred from minutes");
var startText = "2026-11-25T09:00";
var startUtc = TaskSchedule.StartUtc(startText)!.Value;
Check(TaskSchedule.ScheduleLabel(startUtc, startUtc.AddMinutes(-1)) == "Scheduled", "Future schedule remains scheduled");
Check(TaskSchedule.SameStart(startText, startUtc.AddHours(8).ToString("yyyy-MM-ddTHH:mm")), "Equivalent schedule formats do not incorrectly block ongoing scope updates as past reschedules");
Check(TaskSchedule.ScheduleLabel(startUtc, startUtc) == "Due now", "Schedule is due at the exact start time");
Check(TaskSchedule.ScheduleLabel(startUtc, startUtc.AddMinutes(29)) == "Due now", "Due label covers the grace period");
Check(TaskSchedule.ScheduleLabel(startUtc, startUtc.AddMinutes(30)) == "Schedule overdue", "Schedule becomes overdue at 30 minutes without progress");
Check(TaskSchedule.ReminderKind(startUtc, startUtc.AddHours(-25)) is null, "No premature reminder before 24 hours");
Check(TaskSchedule.ReminderKind(startUtc, startUtc.AddHours(-24)) == "schedule_24h", "24-hour reminder window includes its boundary");
Check(TaskSchedule.ReminderKind(startUtc, startUtc.AddHours(-1)) == "schedule_1h", "One-hour reminder takes priority over 24-hour window");
Check(TaskSchedule.ReminderKind(startUtc, startUtc.AddMinutes(10)) is null, "No late upcoming reminder during the grace period");
Check(TaskSchedule.ReminderKind(startUtc, startUtc.AddMinutes(30)) == "schedule_overdue", "Overdue reminder begins at grace boundary");
var oldSchedule = new MarketplaceTask { Status = "assigned", ScheduleText = startText };
Check(!TaskSchedule.TravelOpen(oldSchedule, startUtc.AddMinutes(-61)), "Travel remains blocked more than one hour before arrival");
Check(TaskSchedule.TravelOpen(oldSchedule, startUtc.AddHours(-1)), "Travel opens exactly one hour before arrival");
Check(TaskSchedule.TravelOpen(oldSchedule, startUtc.AddHours(1)), "Late assigned job can still start travel without automatic cancellation");
Check(!TaskSchedule.TravelOpen(new MarketplaceTask { Status = "completed", ScheduleText = startText }, startUtc), "Closed work cannot open travel");
Check(!TaskSchedule.TravelOpen(new MarketplaceTask { Status = "assigned", ScheduleText = "To be scheduled" }, startUtc), "Unknown schedule does not invent a travel opening time");
Check(!JobTypes.ValidSchedule("short", startText, null), "Every new short job needs expected finish");
Check(!JobTypes.ValidSchedule("long", "To be scheduled", startUtc.AddHours(2)), "Unknown starts cannot pass schedule validation");
var cancelRequest = new TaskProgressEvent { Status = TaskProgress.CancellationRequested, CreatedAtUtc = startUtc };
Check(!JobCancellation.FollowUpDue(cancelRequest, startUtc.AddHours(24).AddSeconds(-1)), "Cancellation escalation waits for full 24 hours");
Check(JobCancellation.FollowUpDue(cancelRequest, startUtc.AddHours(24)), "Pending cancellation can escalate at 24 hours");
Check(!JobCancellation.FollowUpDue(new TaskProgressEvent { Status = TaskProgress.CancellationDeclined, CreatedAtUtc = startUtc }, startUtc.AddDays(2)), "Decided cancellation does not escalate");
Check(CompletionReminderWorker.Message("long").Contains("remains locked") && !CompletionReminderWorker.Message("long").Contains("already accept"), "Long-job completion reminder matches work lock");
Check(CompletionReminderWorker.Message("multi-day").Contains("remains locked"), "Multi-day completion reminder matches work lock");
Check(CompletionReminderWorker.Message("short").Contains("already accept"), "Short-job reminder permits other work");
Check(ScheduleReminderEmailWorker.StillRelevant("assigned", 2, "schedule_1h", startUtc, startUtc.AddMinutes(-30), 2, true), "Confirmed due schedule email may be sent");
Check(!ScheduleReminderEmailWorker.StillRelevant("completed", 2, "schedule_1h", startUtc, startUtc.AddMinutes(-30), 2, true), "Completed work skips queued reminders");
Check(!ScheduleReminderEmailWorker.StillRelevant("assigned", 2, "schedule_1h", startUtc, startUtc.AddMinutes(-30), 3, true), "Reschedule invalidates old queued reminder");
Check(!ScheduleReminderEmailWorker.StillRelevant("assigned", 2, "schedule_1h", startUtc, startUtc.AddMinutes(-30), 2, false), "Unconfirmed new agreement blocks reminders");
Check(!ScheduleReminderEmailWorker.StillRelevant("assigned", 2, "schedule_24h", startUtc, startUtc.AddMinutes(-30), 2, true), "Missed 24-hour notice is not sent during 1-hour window");
Check(ScheduleReminderEmailWorker.RetryAt(1, startUtc) == startUtc.AddMinutes(2) && ScheduleReminderEmailWorker.RetryAt(12, startUtc) == startUtc.AddHours(1), "Email retry has bounded exponential backoff");
Check(new LoginCodeWaitException(45).RetrySeconds == 45 && !new LoginCodeWaitException(45).RequestLimit, "OTP cooldown carries exact wait independently of email failure");
Check(new LoginCodeWaitException(900, true).RequestLimit, "OTP request limit is classified separately from cooldown");
Check(OtpResendPolicy.DelaySeconds(1) == 10 && OtpResendPolicy.DelaySeconds(2) == 15 && OtpResendPolicy.DelaySeconds(3) == 20 && OtpResendPolicy.DelaySeconds(4) == 25, "OTP resend wait increases 10, 15, 20, 25 seconds");
Check(LoginSecurity.ResendCooldown(startUtc, startUtc.AddSeconds(9), 1) == 1 && LoginSecurity.ResendCooldown(startUtc, startUtc.AddSeconds(10), 1) == 0, "First resend opens exactly at ten seconds");
Check(LoginSecurity.ResendCooldown(startUtc, startUtc.AddSeconds(10), 2) == 5, "Second resend still waits until fifteen seconds");
Check(RegistrationVerification.Cooldown(new RegistrationEmailCode { LastSentUtc = startUtc, SendCount = 3 }, startUtc.AddSeconds(10)) == 10, "Registration uses the same progressive resend policy");
Check(RegistrationVerification.SendWaitSeconds(new RegistrationEmailCode { LastSentUtc = startUtc, SendCount = 5, SendWindowUtc = startUtc }, startUtc.AddSeconds(30)) == 870, "Five-send safety limit remains independent of shorter cooldowns");
Check(PasswordResetSendState.WaitSeconds(new PasswordResetSendState { LastSentUtc = startUtc, WindowUtc = startUtc, SendCount = 2 }, startUtc.AddSeconds(10)) == 5, "Password reset uses a persisted progressive wait");
Check(PasswordResetSendState.Key("Person@Example.test") == PasswordResetSendState.Key("person@example.test"), "Changing email case cannot bypass the reset resend limit");
Check(TaskSchedule.DisplayStatus(oldSchedule, startUtc.AddDays(1)) == "Schedule overdue" && oldSchedule.Status == "assigned", "Overdue is a display label and never automatically cancels or completes work");
Check(TaskSchedule.DisplayStatus(new MarketplaceTask { Status = "in-progress", ScheduleText = startText }, startUtc.AddDays(1)) == "Work in progress", "Started work is not marked as a missed schedule");
var upcomingWork = new tts.Pages.DashboardModel.ScheduledWork(2, "Future", startUtc.AddDays(1).AddHours(8).ToString("yyyy-MM-ddTHH:mm"));
var overdueWork = new tts.Pages.DashboardModel.ScheduledWork(1, "Missed", startText);
Check(tts.Pages.DashboardModel.SelectScheduledWork(new[] { upcomingWork, overdueWork }, startUtc.AddHours(1)) is { Id: 1, Status: "Schedule overdue" }, "Home prioritizes missed work over the next upcoming schedule");
Check(tts.Pages.DashboardModel.SelectScheduledWork(new[] { upcomingWork }, startUtc.AddHours(1)) is { Id: 2, Status: "Scheduled" }, "Home displays the next schedule after overdue work is resolved");
Check(tts.Pages.DashboardModel.SelectScheduledWork(new[] { new tts.Pages.DashboardModel.ScheduledWork(3, "Unset", "To be scheduled") }, startUtc) is { Status: "Schedule needed" }, "Accepted work missing a date stays visible with a clear next step");
Check(new tts.Pages.DashboardModel.ScheduledWork(3, "Accepted", startText).Confirmation == "Work details needed", "Accepted work is visible before agreement setup");
Check(new tts.Pages.DashboardModel.ScheduledWork(3, "Accepted", startText) { HasAgreement = true, ClientConfirmed = true }.Confirmation == "Your confirmation needed", "Home identifies freelancer confirmation as the next step");
Check(new tts.Pages.DashboardModel.ScheduledWork(3, "Accepted", startText) { HasAgreement = true, FreelancerConfirmed = true }.Confirmation == "Waiting for client confirmation", "Home identifies the other participant's pending confirmation");
Check(TaskSchedule.Display(startText).Contains("Nov 25, 2026") && !TaskSchedule.Display(startText).Contains("T09"), "Native dates display as human-readable Philippine time");
Check(JobTypes.ValidSchedule("short", startText, startUtc.AddHours(2), true), "Short job may end later on the same Philippine date");
Check(JobTypes.ValidSchedule("long", startText, startUtc.AddHours(8), true), "Long job may end on the same Philippine date");
Check(!JobTypes.ValidSchedule("short", startText, startUtc.AddDays(1), true), "Same-day job cannot cross into another date");
Check(!JobTypes.ValidSchedule("multi-day", startText, startUtc.AddHours(2), true), "Multi-day job needs a later finish date");
Check(JobTypes.ValidSchedule("multi-day", startText, startUtc.AddDays(2), true), "Multi-day date range is supported");
Check(!JobTypes.ValidSchedule("multi-day", startText, null), "Multi-day post cannot omit its finish date");
Check(!JobTypes.ValidSchedule("long", startText, null, true), "Final agreement requires a finish for all job types");
Check(!JobTypes.ValidSchedule("short", startText, startUtc.AddMinutes(-1), true), "Finish cannot precede start");
Check(!JobTypes.ValidSchedule("multi-day", "To be scheduled", startUtc.AddDays(2)), "Multi-day cannot use an unparseable start");
Check(JobTypes.EndUtc(new DateTime(2026,11,25,9,0,0)) == startUtc, "Schedule conversion uses Philippine time");
Check(JobTypes.Overlaps(startUtc, startUtc.AddDays(2), startUtc.AddHours(6), startUtc.AddHours(8)), "Multi-day period blocks an intersecting job");
Check(!JobTypes.Overlaps(startUtc, startUtc.AddHours(2), startUtc.AddHours(2), startUtc.AddHours(4)), "Back-to-back jobs do not overlap");
Check(!AgreementRules.SameTerms(new WorkAgreement { JobType="short" }, new WorkAgreement { JobType="long" }), "Job type changes require new agreement terms");
Console.WriteLine($"All {checks} job-flow and job-type checks passed.");
foreach (var type in new[] { "short", "long", "multi-day" })
{
    Check(!JobAcceptanceRules.RequiresCompletion(type, "assigned"), $"Scheduled {type} does not lock new acceptance before starting");
    foreach (var status in JobAcceptanceRules.StartedStates)
        Check(JobAcceptanceRules.RequiresCompletion(type, status) == (type != "short"), $"{type} at {status} has the correct new-work restriction");
    Check(!JobAcceptanceRules.RequiresCompletion(type, "completed") && !JobAcceptanceRules.RequiresCompletion(type, "cancelled"), $"Completed or approved-cancelled {type} releases the lock");
}
Check(JobAcceptanceRules.RequiresCompletion("long", "awaiting-confirmation"), "Long job stays locked until client confirmation");
Check(JobAcceptanceRules.RequiresCompletion("multi-day", "disputed", true), "Started multi-day job stays locked when paused/disputed");
Check(!JobAcceptanceRules.RequiresCompletion("multi-day", "disputed", false), "Pre-start dispute does not pretend the job already started");
Check(!JobAcceptanceRules.RequiresCompletion(null, "in-progress"), "Historical unclassified work is not silently inferred as Long Job");
Console.WriteLine($"All {checks} workflow checks passed.");
using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseMySql("Server=127.0.0.1;Database=unused;User ID=unused",new MySqlServerVersion(new Version(8,0,36))).Options))
{
    var sql = JobAcceptanceRules.BlockingTasks(db,42).ToQueryString();
    var mailEntity = db.Model.FindEntityType(typeof(ScheduleReminderEmail))!;
    Check(mailEntity.GetTableName() == "schedule_reminder_emails" && mailEntity.GetIndexes().Any(index => index.IsUnique && index.Properties.Count == 4), "Reminder outbox has a unique task/user/agreement/window key");
    Check(db.Set<ScheduleReminderEmail>().Where(e => e.Status == "pending" && e.NextAttemptUtc < startUtc).ToQueryString().Contains("schedule_reminder_emails"), "Pending email query translates without a live database");
    Check(tts.Pages.MarketTasksModel.CanAdjustSchedule(new MarketplaceTask { ClientId = 42, Status = "open" }, 42), "Owner can correct a public schedule before acceptance");
    Check(tts.Pages.MarketTasksModel.CanAdjustSchedule(new MarketplaceTask { ClientId = 42, Status = "booking-pending" }, 42), "Owner can correct a pending direct booking schedule");
    Check(!tts.Pages.MarketTasksModel.CanAdjustSchedule(new MarketplaceTask { ClientId = 42, Status = "open" }, 43), "Another account cannot change a pending schedule");
    Check(!tts.Pages.MarketTasksModel.CanAdjustSchedule(new MarketplaceTask { ClientId = 42, Status = "booking-pending" }, 43), "Another account cannot change a direct booking schedule");
    Check(!tts.Pages.MarketTasksModel.CanAdjustSchedule(new MarketplaceTask { ClientId = 42, Status = "assigned" }, 42), "Accepted schedules must use bilateral agreement instead");
    var trackingSql = TaskTracking.VisibleTasks(db.MarketplaceTasks, 3).ToQueryString();
    Check(trackingSql.Contains("awaiting-confirmation") && trackingSql.Contains("completed") && trackingSql.Contains("3"), "Tracking history filter translates to database SQL");
    Check(sql.Contains("awaiting-confirmation") && sql.Contains("multi-day") && sql.Contains("accepted") && sql.Contains("42"), "Database restriction query translates and scopes the accepted freelancer and full completion lifecycle");
    Check(JobAcceptanceRules.BlockingTasks(db,42,7).ToQueryString().Contains("7"), "Current job can be excluded when continuing its own progress");
}
Console.WriteLine($"All {checks} workflow and restriction checks passed. No live database writes.");
