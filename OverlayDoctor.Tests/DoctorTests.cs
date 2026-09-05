using OverlayDoctor;
using Xunit;

public class DoctorTests
{
    private static readonly Report Fine = new(true, true, "fine");
    private static readonly Report Unwell = new(true, false, "stalled");
    private static readonly Report Missing = Report.Absent("not loaded");

    [Fact]
    public void One_press_restarts_both_layers_parser_first()
    {
        Assert.Equal(new[] { Step.RestartParser, Step.RestartRenderer }, Doctor.Plan(Fine, Fine));
        Assert.Equal(new[] { Step.RestartParser, Step.RestartRenderer }, Doctor.Plan(Unwell, Fine));
        Assert.Equal(new[] { Step.RestartParser, Step.RestartRenderer }, Doctor.Plan(Fine, Unwell));
    }

    [Fact]
    public void A_missing_plugin_is_loaded_instead_of_restarted()
    {
        Assert.Equal(new[] { Step.LoadIinact, Step.RestartRenderer }, Doctor.Plan(Missing, Fine));
        Assert.Equal(new[] { Step.RestartParser, Step.LoadBrowsingway }, Doctor.Plan(Fine, Missing));
        Assert.Equal(new[] { Step.LoadIinact, Step.LoadBrowsingway }, Doctor.Plan(Missing, Missing));
    }

    [Fact]
    public void The_summary_names_both_layers()
    {
        Assert.Equal("IINACT: stalled | Browsingway: fine", Doctor.Summary(Unwell, Fine));
    }

    [Fact]
    public void Every_step_has_words()
    {
        foreach (var step in Enum.GetValues<Step>())
            Assert.DoesNotContain(step.ToString(), Doctor.Describe(step));
    }
}
