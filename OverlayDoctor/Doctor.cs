namespace OverlayDoctor;

public enum Step
{
    LoadIinact,
    RestartParser,
    LoadBrowsingway,
    RestartRenderer,
}

/// <summary>What one plugin says about itself, or that it could not be asked.</summary>
public readonly record struct Report(bool Loaded, bool Healthy, string Status)
{
    public static Report Absent(string detail) => new(false, false, detail);
}

public static class Doctor
{
    public static IReadOnlyList<Step> Plan(Report iinact, Report browsingway)
    {
        var steps = new List<Step>();
        // Both claiming fine while the user still pressed fix means a stall neither self-check caught;
        // the parser's own watchdog is the same judge, so restart both rather than trust it.
        var nothingAdmitsTrouble = iinact is { Loaded: true, Healthy: true } && browsingway is { Loaded: true, Healthy: true };

        if (!iinact.Loaded)
            steps.Add(Step.LoadIinact);
        else if (!iinact.Healthy || nothingAdmitsTrouble)
            steps.Add(Step.RestartParser);

        if (!browsingway.Loaded)
            steps.Add(Step.LoadBrowsingway);
        else if (!browsingway.Healthy || nothingAdmitsTrouble)
            steps.Add(Step.RestartRenderer);
        return steps;
    }

    public static string Summary(Report iinact, Report browsingway) =>
        $"IINACT: {iinact.Status} | Browsingway: {browsingway.Status}";

    public static string Describe(Step step) => step switch
    {
        Step.LoadIinact => "loading IINACT",
        Step.RestartParser => "restarting the parser",
        Step.LoadBrowsingway => "loading Browsingway",
        Step.RestartRenderer => "respawning the renderer",
        _ => step.ToString(),
    };
}
