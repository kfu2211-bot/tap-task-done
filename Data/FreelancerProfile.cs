namespace tts.Data;

public sealed class FreelancerProfile
{
    public int UserId { get; set; }
    public string Service { get; set; } = "";
    public string Location { get; set; } = "";
    public int? Rate { get; set; }
    public string WorkRole { get; set; } = "client";
    public string Availability { get; set; } = "Available";
    public string AvailableDays { get; set; } = "";
}
