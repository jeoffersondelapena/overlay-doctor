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
    // Always both, parser first; trusting the self-checks cost a second press.
    public static IReadOnlyList<Step> Plan(Report iinact, Report browsingway) => new[]
    {
        iinact.Loaded ? Step.RestartParser : Step.LoadIinact,
        browsingway.Loaded ? Step.RestartRenderer : Step.LoadBrowsingway,
    };

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
