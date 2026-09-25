namespace QuickCalc.App;

internal static class DiagnosticFileLogger
{
    private static readonly object Sync = new();
    public static string LogPath => Path.Combine(AppContext.BaseDirectory, "quickcalc-error.log");

    public static void Write(Exception exception, string area)
    {
        try
        {
            lock (Sync)
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {area}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never cause a second application failure.
        }
    }
}
