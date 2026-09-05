using OverlayDoctor;
using Xunit;

public class DiagLogTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 22, 0, 0);

    [Fact]
    public void File_names_carry_the_start_time_and_pid()
    {
        Assert.Equal("doctor-20260905-215211-364.log", DiagLog.FileName(new DateTime(2026, 9, 5, 21, 52, 11), 364));
    }

    [Fact]
    public void Only_files_past_the_retention_window_are_pruned()
    {
        var files = new[] { "/d/doctor-20260905-195211-364.log", "/d/doctor-20260829-090000-12.log", "/d/doctor-20260828-090000-12.log", "/d/notes.log" };
        Assert.Equal(new[] { "/d/doctor-20260828-090000-12.log" }, DiagLog.FilesToPrune(files, Now));
    }

    [Fact]
    public void Writes_land_in_the_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "doctor-diag-test-" + Guid.NewGuid());
        using (var log = new DiagLog(dir, Now, 42))
            log.Write("hello");
        Assert.Contains("] hello", File.ReadAllText(Path.Combine(dir, "doctor-20260905-220000-42.log")));
        Directory.Delete(dir, true);
    }
}
