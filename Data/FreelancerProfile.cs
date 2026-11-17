namespace tts.Data;

public sealed class FreelancerProfile
{
    public int UserId { get; set; }
    public string Service { get; set; } = "";
    public string? ServiceSpecialties { get; set; }
    public string Location { get; set; } = "";
    public int? Rate { get; set; }
    public int? RateMaximum { get; set; }
    public static int? GetServiceRate(int? rate, int? rateMaximum) =>
        rate is > 0 && !(rateMaximum > rate) ? rate : null;

    public string WorkRole { get; set; } = "client";
    public string Availability { get; set; } = "Available";
    public TimeOnly? PreferredStart { get; set; }
    public TimeOnly? PreferredEnd { get; set; }
    public string AvailableDays { get; set; } = "";
}
