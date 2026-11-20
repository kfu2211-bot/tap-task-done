using System.Text;
using tts.Data;

var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    checks++;
    Console.WriteLine("PASS " + label);
}

Check(TaskProgress.ValidCompletion("Work completed.", false), "Completion note alone is accepted");
Check(TaskProgress.ValidCompletion(null, true), "Video alone is accepted");
Check(TaskProgress.ValidCompletion("Finished", true), "Note and video together are accepted");
Check(!TaskProgress.ValidCompletion("   ", false), "Empty completion is rejected");
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
Check(!JobCancellation.CanRequest("completed") && !JobCancellation.CanRequest("assigned"), "Closed jobs and pre-travel withdrawal use correct paths");
Check(!TaskProgress.CanAdvance("completed", "finish", false, true, true), "Completed job cannot be finished again");
Check(!TaskProgress.CanAdvance("in-progress", "confirm", true, false, true), "Client confirmation requires submitted completion");
Console.WriteLine($"All {checks} job-flow checks passed.");
