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
        if (!iinact.Loaded)
            steps.Add(Step.LoadIinact);
        else if (!iinact.Healthy)
            steps.Add(Step.RestartParser);

        // A parser restart makes the overlays reconnect on their own, so the renderer is respawned
        // only when it reports trouble or when the parser side needed nothing at all.
        if (!browsingway.Loaded)
            steps.Add(Step.LoadBrowsingway);
        else if (!browsingway.Healthy || steps.Count == 0)
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
