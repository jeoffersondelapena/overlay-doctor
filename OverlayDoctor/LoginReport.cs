namespace OverlayDoctor;

public static class LoginReport
{
    public const double HealthWaitSeconds = 90;
    public const double SettleAfterZoneSeconds = 5;
    public const double SettleCapSeconds = 45;

    /// <summary>Print once both layers answered (or the wait ran out) and the zone's own login lines have landed.</summary>
    public static bool ReadyToPrint(double sinceLogin, double? sinceZone, bool bothHealthy)
    {
        var healthSettled = bothHealthy || sinceLogin >= HealthWaitSeconds;
        var linesSettled = (sinceZone is >= SettleAfterZoneSeconds) || sinceLogin >= SettleCapSeconds;
        return healthSettled && linesSettled;
    }

    public static string Line(Report iinact, Report browsingway, IReadOnlyList<string> attention, bool waitedOut = false)
    {
        var fine = iinact.Loaded && iinact.Healthy && browsingway.Loaded && browsingway.Healthy;
        if (fine && attention.Count == 0)
            return $"Overlay Doctor: all good; parser healthy, {browsingway.Status}.";
        var parts = new List<string>();
        if (!fine)
        {
            var parser = iinact.Loaded ? (iinact.Healthy ? "healthy" : "unwell") : iinact.Status;
            var suffix = waitedOut ? $" (still not ready after {HealthWaitSeconds:F0} s)" : "";
            parts.Add($"IINACT {parser} | Browsingway {browsingway.Status}{suffix}; use /overlays fix");
        }
        parts.AddRange(attention);
        return "Overlay Doctor: attention. " + string.Join(" ", parts.Select(p => p.TrimEnd('.') + "."));
    }
}
