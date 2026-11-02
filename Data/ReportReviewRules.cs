namespace tts.Data;

public static class ReportReviewRules
{
    public static bool Valid(string? status, string note, string message, bool reviewed) =>
        status is "reviewing" or "resolved" or "dismissed" &&
        note.Length <= 1000 && message.Length is >= 10 and <= 1000 &&
        (status == "reviewing" || (reviewed && note.Length >= 10));
}
