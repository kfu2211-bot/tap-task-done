namespace tts.Data;

public static class DisplayTime
{
    // Database event timestamps are stored in UTC. Philippine Standard Time is UTC+8.
    public static string Philippine(DateTime utc) => utc.AddHours(8).ToString("MMM d, yyyy h:mm tt");
    public static string PhilippineDate(DateTime utc) => utc.AddHours(8).ToString("MMM d, yyyy");
}
