namespace tts.Data;

public sealed class UserContact
{
    public int UserId { get; set; }
    public string PhoneNumber { get; set; } = "";
    public bool ShareWithAssignedUser { get; set; }
}
