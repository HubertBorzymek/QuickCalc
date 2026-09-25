using System.Globalization;

namespace QuickCalc.Core;

public sealed record CalculationResult(string Text, Quantity Value, bool IsRelative);

public sealed class ExpressionEvaluator
{
    public CalculationResult Evaluate(string expression, string? selectedText = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new CalculationException("Wpisz wyrażenie.");
        ParsedSelection.TryParse(selectedText, out var selection);
        var trimmed = expression.Trim();
        var relative = IsRelative(trimmed, selection is not null);
        if (relative && selection is null)
            throw new CalculationException("Działanie względne wymaga zaznaczonej wartości liczbowej.");

        var parser = new Parser(relative ? "0" + trimmed : trimmed, relative, selection);
        var value = relative ? parser.ApplyRelativeSeed(selection!.Value) : parser.Parse();
        EnsureFinite(value.BaseValue);

        string? outputUnit = value.Dimension == Dimension.Length
            ? selection?.Unit ?? parser.FirstUnit ?? "mm"
            : !relative && parser.FirstUnit is null && selection is not null ? selection.Unit : null;
        var displayValue = value.Dimension == Dimension.Length
            ? value.BaseValue / Units.ToMillimetres(outputUnit!)
            : value.BaseValue;
        var suffix = outputUnit is null ? "" : (selection?.SpaceBeforeUnit == true ? " " : "") + outputUnit;
        return new CalculationResult(Format(displayValue) + suffix, value, relative);
    }

    private static bool IsRelative(string text, bool hasSelection)
    {
        if (text.Length == 0 || text[0] is not ('+' or '-' or '*' or '/')) return false;
        if (text[0] == '-' && !hasSelection) return false;
        return true;
    }

    private static string Format(double value)
    {
        if (Math.Abs(value) < 5e-13) value = 0;
        return value.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    private static void EnsureFinite(double value)
    {
        if (!double.IsFinite(value)) throw new CalculationException("Przepełnienie matematyczne.");
    }

    private sealed class Parser(string text, bool relative, ParsedSelection? selection)
    {
        private int _position;
        private Quantity? _relativeSeed;
        public string? FirstUnit { get; private set; }

        public Quantity Parse()
        {
            var value = ParseAddSubtract();
            SkipWhite();
            if (_position != text.Length) throw Error("Nieobsługiwana składnia");
            return value;
        }

        public Quantity ApplyRelativeSeed(Quantity seed)
        {
            _relativeSeed = seed;
            _position = 0;
            return ParseSeeded();
        }

        private Quantity ParseSeeded()
        {
            SkipWhite();
            var op = text[_position++]; // the synthetic zero is replaced by the selected value
            if (op != '0') throw Error("Błąd działania względnego");
            SkipWhite();
            if (_position >= text.Length) throw Error("Niepełne wyrażenie");
            var initial = text[_position++];
            var right = ParseMultiplyDivide();
            var result = Apply(initial, _relativeSeed!.Value, right);
            while (true)
            {
                SkipWhite();
                if (!Take('+') && !Take('-')) break;
                var nextOp = text[_position - 1];
                result = Apply(nextOp, result, ParseMultiplyDivide());
            }
            SkipWhite();
            if (_position != text.Length) throw Error("Nieobsługiwana składnia");
            return result;
        }

        private Quantity ParseAddSubtract()
        {
            var value = ParseMultiplyDivide();
            while (true)
            {
                SkipWhite();
                if (!Take('+') && !Take('-')) return value;
                var op = text[_position - 1];
                value = Apply(op, value, ParseMultiplyDivide());
            }
        }

        private Quantity ParseMultiplyDivide()
        {
            var value = ParseUnary();
            while (true)
            {
                SkipWhite();
                if (!Take('*') && !Take('/')) return value;
                var op = text[_position - 1];
                value = Apply(op, value, ParseUnary());
            }
        }

        private Quantity ParseUnary()
        {
            SkipWhite();
            if (Take('+')) return ParseUnary();
            if (Take('-')) { var q = ParseUnary(); return q with { BaseValue = -q.BaseValue }; }
            return ParsePrimary();
        }

        private Quantity ParsePrimary()
        {
            SkipWhite();
            if (Take('('))
            {
                var value = ParseAddSubtract();
                SkipWhite();
                if (!Take(')')) throw Error("Brak zamykającego nawiasu");
                return value;
            }

            var start = _position;
            var decimalSeen = false;
            while (_position < text.Length)
            {
                var c = text[_position];
                if (char.IsDigit(c)) { _position++; continue; }
                if ((c == '.' || c == ',') && !decimalSeen) { decimalSeen = true; _position++; continue; }
                break;
            }
            if (start == _position) throw Error("Oczekiwano liczby");
            var raw = text[start.._position].Replace(',', '.');
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                throw Error("Nieprawidłowa liczba");

            var unitStart = _position;
            while (_position < text.Length && char.IsLetter(text[_position])) _position++;
            if (unitStart == _position) return Quantity.Scalar(number);
            var unit = Units.Normalize(text[unitStart.._position]);
            FirstUnit ??= unit;
            return Quantity.Length(number * Units.ToMillimetres(unit));
        }

        private Quantity Apply(char op, Quantity left, Quantity right)
        {
            if (op is '+' or '-')
            {
                if (left.Dimension != right.Dimension)
                {
                    if (relative && selection is not null && left.Dimension == Dimension.Length && right.Dimension == Dimension.Scalar)
                        right = Quantity.Length(right.BaseValue * Units.ToMillimetres(selection.Unit!));
                    else throw Error("Nie można dodawać wartości o różnych wymiarach");
                }
                return new Quantity(op == '+' ? left.BaseValue + right.BaseValue : left.BaseValue - right.BaseValue, left.Dimension);
            }
            if (op == '*')
            {
                if (left.Dimension == Dimension.Length && right.Dimension == Dimension.Length)
                    throw Error("Mnożenie dwóch długości nie jest obsługiwane");
                return new Quantity(left.BaseValue * right.BaseValue,
                    left.Dimension == Dimension.Length || right.Dimension == Dimension.Length ? Dimension.Length : Dimension.Scalar);
            }
            if (right.BaseValue == 0) throw Error("Dzielenie przez zero");
            if (left.Dimension == Dimension.Scalar && right.Dimension == Dimension.Length)
                throw Error("Dzielenie liczby przez długość nie jest obsługiwane");
            return new Quantity(left.BaseValue / right.BaseValue,
                left.Dimension == right.Dimension ? Dimension.Scalar : left.Dimension);
        }

        private bool Take(char expected)
        {
            if (_position >= text.Length || text[_position] != expected) return false;
            _position++;
            return true;
        }

        private void SkipWhite() { while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++; }
        private CalculationException Error(string message) => new($"{message} (pozycja {_position + 1}).");
    }
}
