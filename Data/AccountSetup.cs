namespace tts.Data;

public sealed record AccountSetupStep(string Title, string Description, string Status, string Action, string Url, bool Complete, bool Available = true);

public sealed record AccountSetup(IReadOnlyList<AccountSetupStep> Steps)
{
    public int Completed => Steps.Count(step => step.Complete);
    public bool Complete => Completed == Steps.Count;
    public bool NeedsAction => Steps.Any(step => step.Available && !step.Complete && step.Status != "Under review");

    public static AccountSetup Create(string? role, IEnumerable<string> acceptedRoles, string? identityStatus, bool hasPhone,
        bool roleAvailable = true, bool identityAvailable = true, bool phoneAvailable = true)
    {
        var accepted = acceptedRoles.ToHashSet(StringComparer.Ordinal);
        var roleSet = roleAvailable && (role switch
        {
            "client" => accepted.Contains("client"),
            "freelancer" => accepted.Contains("freelancer"),
            "both" => accepted.Contains("client") && accepted.Contains("freelancer"),
            _ => false
        });
        var verified = identityAvailable && identityStatus == "approved";
        var pending = identityAvailable && identityStatus == "pending";
        var identityLabel = !identityAvailable ? "Unavailable" : identityStatus switch
        {
            "approved" => "Verified", "pending" => "Under review", "resubmit" => "Resubmit ID",
            "rejected" => "Needs attention", _ => "Not verified"
        };
        return new AccountSetup([
            new("Piliin ang work role", "Client, Freelancer, o pareho.", !roleAvailable ? "Unavailable" : roleSet ? "Role set" : "Not set",
                roleSet ? "View role" : "Set role", "/Settings#work-preferences", roleSet, roleAvailable),
            new("Verify your identity", pending ? "Naipasa na ang ID. Hintayin ang admin review." : "Mag-upload ng valid ID at kumuha ng selfie.", identityLabel,
                verified || pending ? "View status" : identityStatus is "resubmit" or "rejected" ? "Review request" : "Verify now", "/IdentityVerification", verified, identityAvailable),
            new("Idagdag ang mobile number", "Ikaw ang pipili kung kailan ito ibabahagi.", !phoneAvailable ? "Unavailable" : hasPhone ? "Added" : "Not added",
                hasPhone ? "Edit number" : "Add number", "/Settings?view=profile&editPhone=true#contact-details", hasPhone && phoneAvailable, phoneAvailable)
        ]);
    }
}
