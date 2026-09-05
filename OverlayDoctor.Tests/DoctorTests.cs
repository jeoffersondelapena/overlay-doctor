using OverlayDoctor;
using Xunit;

public class DoctorTests
{
    private static readonly Report Fine = new(true, true, "fine");
    private static readonly Report Unwell = new(true, false, "stalled");
    private static readonly Report Missing = Report.Absent("not loaded");

    [Fact]
    public void Both_healthy_means_the_renderer_is_the_only_thing_left_to_try()
    {
        Assert.Equal(new[] { Step.RestartRenderer }, Doctor.Plan(Fine, Fine));
    }

    [Fact]
    public void A_stalled_parser_is_restarted_and_the_renderer_left_alone()
    {
        Assert.Equal(new[] { Step.RestartParser }, Doctor.Plan(Unwell, Fine));
    }

    [Fact]
    public void Both_unwell_means_both_are_restarted_parser_first()
    {
        Assert.Equal(new[] { Step.RestartParser, Step.RestartRenderer }, Doctor.Plan(Unwell, Unwell));
    }

    [Fact]
    public void A_missing_plugin_is_loaded_rather_than_restarted()
    {
        Assert.Equal(new[] { Step.LoadIinact }, Doctor.Plan(Missing, Fine));
        Assert.Equal(new[] { Step.LoadBrowsingway }, Doctor.Plan(Fine, Missing));
        Assert.Equal(new[] { Step.LoadIinact, Step.LoadBrowsingway }, Doctor.Plan(Missing, Missing));
    }

    [Fact]
    public void An_unready_renderer_is_respawned_even_when_the_parser_needed_help()
    {
        Assert.Equal(new[] { Step.RestartParser, Step.RestartRenderer }, Doctor.Plan(Unwell, Unwell));
        Assert.Equal(new[] { Step.LoadIinact, Step.RestartRenderer }, Doctor.Plan(Missing, Unwell));
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
