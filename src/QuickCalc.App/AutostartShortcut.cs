namespace QuickCalc.App;

// Skrot QuickCalc.lnk w folderze Autostart biezacego uzytkownika — ten sam, ktory tworzy
// Install-QuickCalc-Autostart.cmd, wiec menu i skrypty nie tworza duplikatow.
internal static class AutostartShortcut
{
    public static string ShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "QuickCalc.lnk");

    public static string? ReadTarget(string? path = null)
    {
        path ??= ShortcutPath;
        if (!File.Exists(path)) return null;
        dynamic? shell = null;
        try
        {
            shell = CreateShell();
            return (string)shell.CreateShortcut(path).TargetPath;
        }
        finally { Release(shell); }
    }

    public static bool PointsToThisExecutable() =>
        ReadTarget() is { } target && Environment.ProcessPath is { } exe &&
        string.Equals(Path.GetFullPath(target), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase);

    public static void Enable(string? path = null)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Nieznana ścieżka programu.");
        dynamic? shell = null;
        try
        {
            shell = CreateShell();
            var shortcut = shell.CreateShortcut(path ?? ShortcutPath);
            shortcut.TargetPath = exe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exe);
            shortcut.IconLocation = exe;
            shortcut.Save();
        }
        finally { Release(shell); }
    }

    public static void Disable()
    {
        if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
    }

    private static dynamic CreateShell() =>
        Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Brak WScript.Shell."))!;

    private static void Release(object? comObject)
    {
        if (comObject is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(comObject);
    }
}
