namespace tts.Data;

public static class OtpResendPolicy
{
    // sentCount includes the initial email: 1 -> 10s, 2 -> 15s, 3 -> 20s.
    public static int DelaySeconds(int sentCount) => (int)Math.Min(int.MaxValue, 10L + 5L * Math.Max(0L, (long)sentCount - 1));
    public static int Remaining(DateTime lastSentUtc, DateTime now, int sentCount) =>
        Math.Max(0, (int)Math.Ceiling((lastSentUtc.AddSeconds(DelaySeconds(sentCount)) - now).TotalSeconds));
}
