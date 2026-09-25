using System.Globalization;

namespace QuickCalc.Core;

public enum Dimension { Scalar, Length }

public readonly record struct Quantity(double BaseValue, Dimension Dimension)
{
    public static Quantity Scalar(double value) => new(value, Dimension.Scalar);
    public static Quantity Length(double millimetres) => new(millimetres, Dimension.Length);
}

public sealed record ParsedSelection(Quantity Value, string Unit, bool SpaceBeforeUnit)
{
    public static bool TryParse(string? text, out ParsedSelection? selection)
    {
        selection = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var match = System.Text.RegularExpressions.Regex.Match(text,
            @"^\s*([+-]?(?:\d+(?:[\.,]\d*)?|[\.,]\d+))([ \t]*)(mm|mil|in|inch)\s*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        if (!double.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) return false;
        var unit = Units.Normalize(match.Groups[3].Value);
        selection = new ParsedSelection(Quantity.Length(number * Units.ToMillimetres(unit)), unit,
            match.Groups[2].Value.Length > 0);
        return true;
    }
}

internal static class Units
{
    public static string Normalize(string unit) => unit.ToLowerInvariant() switch
    {
        "inch" => "in",
        "mm" or "mil" or "in" => unit.ToLowerInvariant(),
        _ => throw new CalculationException($"Nieobsługiwana jednostka: {unit}.")
    };

    public static double ToMillimetres(string unit) => Normalize(unit) switch
    {
        "mm" => 1d,
        "mil" => 0.0254d,
        "in" => 25.4d,
        _ => throw new CalculationException("Nieobsługiwana jednostka.")
    };
}
