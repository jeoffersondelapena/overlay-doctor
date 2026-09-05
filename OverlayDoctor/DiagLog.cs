using System.Globalization;
using System.Text;

namespace OverlayDoctor;

// Two clients share dalamud.log; the one that loses that file keeps no record.
public sealed class DiagLog : IDisposable
{
    public const int RetentionDays = 7;

    private readonly object gate = new();
    private readonly StreamWriter writer;

    public string Path { get; }

    public DiagLog(string directory, DateTime startedAt, int processId)
    {
        Directory.CreateDirectory(directory);
        foreach (var stale in FilesToPrune(Directory.GetFiles(directory, "doctor-*.log"), DateTime.Now))
        {
            try { File.Delete(stale); }
            catch (IOException) { }
        }
        Path = System.IO.Path.Combine(directory, FileName(startedAt, processId));
        var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public static string FileName(DateTime startedAt, int processId) => $"doctor-{startedAt:yyyyMMdd-HHmmss}-{processId}.log";

    /// <summary>Files older than the retention window; names that do not carry a date are kept.</summary>
    public static IEnumerable<string> FilesToPrune(IEnumerable<string> paths, DateTime now)
    {
        foreach (var path in paths)
        {
            var parts = System.IO.Path.GetFileName(path).Split('-');
            if (parts.Length < 3 || !DateTime.TryParseExact(parts[1], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                continue;
            if ((now.Date - day).TotalDays > RetentionDays)
                yield return path;
        }
    }

    public void Write(string message)
    {
        lock (gate)
        {
            try { writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}"); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        }
    }

    public void Dispose()
    {
        lock (gate)
            writer.Dispose();
    }
}
