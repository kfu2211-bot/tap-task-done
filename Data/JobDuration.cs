namespace tts.Data;

public static class JobDuration
{
    public static bool Valid(int? minutes) => minutes is null or >= 15 and <= 43200;
    public static string Display(int? minutes) => minutes is null ? "Not specified"
        : minutes % 60 == 0 ? $"{minutes / 60} hour(s)" : $"{minutes / 60}h {minutes % 60}m";
}
