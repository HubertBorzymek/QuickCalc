namespace QuickCalc.App;

internal static class AppIcon
{
    private const string ResourceName = "QuickCalc.App.QuickCalc.ico";

    // Wybiera klatke ICO dopasowana do rozmiaru (np. ostre 16 px w zasobniku przy 100% DPI).
    public static Icon Load(Size size)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName);
        return stream is null ? (Icon)SystemIcons.Application.Clone() : new Icon(stream, size);
    }
}
