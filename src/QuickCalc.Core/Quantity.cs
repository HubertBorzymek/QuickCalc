using System.Globalization;
using System.Text.RegularExpressions;

namespace QuickCalc.Core;

public enum Dimension
{
    Scalar, Length, Mass, Time, ElectricCurrent, Temperature, Amount, LuminousIntensity,
    Voltage, Resistance, Capacitance, Inductance, Frequency, Power, Force, Pressure, Energy,
    ElectricCharge, Conductance, MagneticFlux, MagneticFluxDensity
}

public readonly record struct Quantity(double BaseValue, Dimension Dimension)
{
    public static Quantity Scalar(double value) => new(value, Dimension.Scalar);
    public static Quantity Of(double baseValue, Dimension dimension) => new(baseValue, dimension);
}

public sealed record ParsedSelection(Quantity Value, string? Unit, bool SpaceBeforeUnit)
{
    private static readonly Regex Pattern = new(
        @"^\s*([+-]?(?:\d+(?:[\.,]\d*)?|[\.,]\d+))([ \t]*)([\p{L}µμΩ]+)?\s*$",
        RegexOptions.Compiled);

    public static bool TryParse(string? text, out ParsedSelection? selection)
    {
        selection = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var match = Pattern.Match(text);
        if (!match.Success || !double.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) return false;

        if (!match.Groups[3].Success)
        {
            selection = new ParsedSelection(Quantity.Scalar(number), null, false);
            return true;
        }
        try
        {
            var unit = Units.Resolve(match.Groups[3].Value);
            selection = new ParsedSelection(Quantity.Of(number * unit.ToBaseFactor, unit.Dimension),
                unit.Symbol, match.Groups[2].Value.Length > 0);
            return true;
        }
        catch (CalculationException) { return false; }
    }
}

internal sealed record UnitDefinition(string Symbol, Dimension Dimension, double ToBaseFactor, bool IsSi);

internal static class Units
{
    private static readonly Dictionary<string, UnitDefinition> Exact = new(StringComparer.Ordinal)
    {
        ["m"] = new("m", Dimension.Length, 1, true), ["s"] = new("s", Dimension.Time, 1, true),
        ["g"] = new("g", Dimension.Mass, 1e-3, true), ["A"] = new("A", Dimension.ElectricCurrent, 1, true),
        ["K"] = new("K", Dimension.Temperature, 1, true), ["mol"] = new("mol", Dimension.Amount, 1, true),
        ["cd"] = new("cd", Dimension.LuminousIntensity, 1, true), ["V"] = new("V", Dimension.Voltage, 1, true),
        ["Ω"] = new("Ω", Dimension.Resistance, 1, true), ["F"] = new("F", Dimension.Capacitance, 1, true),
        ["H"] = new("H", Dimension.Inductance, 1, true), ["Hz"] = new("Hz", Dimension.Frequency, 1, true),
        ["W"] = new("W", Dimension.Power, 1, true),
        ["N"] = new("N", Dimension.Force, 1, true), ["Pa"] = new("Pa", Dimension.Pressure, 1, true),
        ["J"] = new("J", Dimension.Energy, 1, true), ["C"] = new("C", Dimension.ElectricCharge, 1, true),
        ["S"] = new("S", Dimension.Conductance, 1, true), ["Wb"] = new("Wb", Dimension.MagneticFlux, 1, true),
        ["T"] = new("T", Dimension.MagneticFluxDensity, 1, true),
        ["in"] = new("in", Dimension.Length, 0.0254, false), ["ft"] = new("ft", Dimension.Length, 0.3048, false),
        ["yd"] = new("yd", Dimension.Length, 0.9144, false), ["mi"] = new("mi", Dimension.Length, 1609.344, false),
        ["mil"] = new("mil", Dimension.Length, 0.0000254, false), ["min"] = new("min", Dimension.Time, 60, false),
        ["h"] = new("h", Dimension.Time, 3600, false)
    };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["inch"] = "in", ["inches"] = "in", ["foot"] = "ft", ["feet"] = "ft",
        ["ohm"] = "Ω", ["ohms"] = "Ω"
    };

    private static readonly (string Symbol, double Factor)[] Prefixes =
    [
        ("da", 1e1), ("Q", 1e30), ("R", 1e27), ("Y", 1e24), ("Z", 1e21), ("E", 1e18),
        ("P", 1e15), ("T", 1e12), ("G", 1e9), ("M", 1e6), ("k", 1e3), ("h", 1e2),
        ("d", 1e-1), ("c", 1e-2), ("m", 1e-3), ("u", 1e-6), ("µ", 1e-6), ("μ", 1e-6),
        ("n", 1e-9), ("p", 1e-12), ("f", 1e-15), ("a", 1e-18), ("z", 1e-21),
        ("y", 1e-24), ("r", 1e-27), ("q", 1e-30)
    ];

    private static readonly HashSet<string> Prefixable = new(StringComparer.Ordinal)
        { "m", "s", "g", "A", "K", "mol", "cd", "V", "Ω", "F", "H", "Hz", "W",
          "N", "Pa", "J", "C", "S", "Wb", "T" };

    public static UnitDefinition Resolve(string input)
    {
        var token = input.Trim();
        if (Aliases.TryGetValue(token, out var alias)) token = alias;
        if (Exact.TryGetValue(token, out var exact)) return exact;
        foreach (var (prefix, factor) in Prefixes)
        {
            if (!token.StartsWith(prefix, StringComparison.Ordinal) || token.Length == prefix.Length) continue;
            var baseToken = token[prefix.Length..];
            if (Aliases.TryGetValue(baseToken, out var baseAlias)) baseToken = baseAlias;
            if (!Prefixable.Contains(baseToken) || !Exact.TryGetValue(baseToken, out var baseUnit)) continue;
            var canonicalPrefix = prefix is "u" or "μ" ? "µ" : prefix;
            return new UnitDefinition(canonicalPrefix + baseUnit.Symbol, baseUnit.Dimension,
                factor * baseUnit.ToBaseFactor, true);
        }
        throw new CalculationException($"Nieobsługiwana jednostka: {input}.");
    }

    public static string ReadableSiUnit(double baseValue, Dimension dimension)
    {
        var magnitude = Math.Abs(baseValue);
        if (dimension == Dimension.Length)
        {
            if (magnitude >= 1000) return "km";
            if (magnitude >= 1) return "m";
            if (magnitude >= 1e-6) return "mm";
            return "µm";
        }
        if (dimension == Dimension.Mass)
        {
            if (magnitude >= 1) return "kg";
            if (magnitude >= 1e-3) return "g";
            return "mg";
        }
        var baseSymbol = dimension switch
        {
            Dimension.Time => "s", Dimension.ElectricCurrent => "A", Dimension.Temperature => "K",
            Dimension.Amount => "mol", Dimension.LuminousIntensity => "cd", Dimension.Voltage => "V",
            Dimension.Resistance => "Ω", Dimension.Capacitance => "F", Dimension.Inductance => "H",
            Dimension.Frequency => "Hz", Dimension.Power => "W", Dimension.Force => "N",
            Dimension.Pressure => "Pa", Dimension.Energy => "J", Dimension.ElectricCharge => "C",
            Dimension.Conductance => "S", Dimension.MagneticFlux => "Wb", Dimension.MagneticFluxDensity => "T",
            _ => throw new CalculationException("Brak jednostki SI dla wyniku.")
        };
        if (dimension is Dimension.Temperature or Dimension.Amount or Dimension.LuminousIntensity || magnitude == 0)
            return baseSymbol;
        var exponent = Math.Clamp((int)Math.Floor(Math.Log10(magnitude) / 3) * 3, -12, 12);
        var prefix = exponent switch
        {
            -12 => "p", -9 => "n", -6 => "µ", -3 => "m", 0 => "",
            3 => "k", 6 => "M", 9 => "G", 12 => "T", _ => ""
        };
        return prefix + baseSymbol;
    }
}
