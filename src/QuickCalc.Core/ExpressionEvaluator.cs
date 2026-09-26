using System.Globalization;

namespace QuickCalc.Core;

public sealed record CalculationResult(string Text, Quantity Value, bool IsRelative);

public sealed class ExpressionEvaluator
{
    public CalculationResult Evaluate(string expression, string? selectedText = null, bool convertToSi = false)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new CalculationException("Wpisz wyrażenie.");
        ParsedSelection.TryParse(selectedText, out var selection);
        var trimmed = expression.Trim();
        var relative = IsRelative(trimmed, selection is not null);
        if (relative && selection is null)
            throw new CalculationException("Działanie względne wymaga zaznaczonej wartości liczbowej.");

        var parser = new Parser(relative ? "0" + trimmed : trimmed, relative, selection);
        var value = relative ? parser.ApplyRelativeSeed(selection!.Value) : parser.Parse();
        if (!relative && value.Dimension == Dimension.Scalar && parser.FirstUnit is null && selection?.Unit is not null)
        {
            var selectedUnit = Units.Resolve(selection.Unit);
            value = Quantity.Of(value.BaseValue * selectedUnit.ToBaseFactor, selectedUnit.Dimension);
        }
        EnsureFinite(value.BaseValue);

        string? outputUnit = null;
        if (value.Dimension != Dimension.Scalar)
            outputUnit = convertToSi
                ? Units.ReadableSiUnit(value.BaseValue, value.Dimension)
                : selection?.Unit ?? parser.FirstUnit ?? Units.ReadableSiUnit(value.BaseValue, value.Dimension);

        var displayValue = outputUnit is null ? value.BaseValue : value.BaseValue / Units.Resolve(outputUnit).ToBaseFactor;
        var spaced = selection?.SpaceBeforeUnit ?? parser.FirstUnitHadSpace;
        var suffix = outputUnit is null ? "" : (spaced ? " " : "") + outputUnit;
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
        if (Math.Abs(value) < 5e-15) value = 0;
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
        public bool FirstUnitHadSpace { get; private set; }

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
            if (text[_position++] != '0') throw Error("Błąd działania względnego");
            SkipWhite();
            if (_position >= text.Length) throw Error("Niepełne wyrażenie");
            var initial = text[_position++];
            var right = ParseMultiplyDivide();
            var result = Apply(initial, _relativeSeed!.Value, right);
            while (true)
            {
                SkipWhite();
                if (!Take('+') && !Take('-')) break;
                var op = text[_position - 1];
                result = Apply(op, result, ParseMultiplyDivide());
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
            if (Take('r') || Take('√'))
            {
                var q = ParseUnary();
                if (q.Dimension != Dimension.Scalar) throw Error("Pierwiastek z jednostki nie jest obsługiwany");
                if (q.BaseValue < 0) throw Error("Pierwiastek z liczby ujemnej");
                return Quantity.Scalar(Math.Sqrt(q.BaseValue));
            }
            return ParsePower();
        }

        private Quantity ParsePower()
        {
            var value = ParsePrimary();
            SkipWhite();
            if (!Take('^')) return value;
            var exponent = ParseUnary();
            if (value.Dimension != Dimension.Scalar || exponent.Dimension != Dimension.Scalar)
                throw Error("Potęgowanie wartości z jednostką nie jest obsługiwane");
            return Quantity.Scalar(Math.Pow(value.BaseValue, exponent.BaseValue));
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

            var whitespaceStart = _position;
            SkipWhite();
            var unitStart = _position;
            while (_position < text.Length && IsUnitCharacter(text[_position])) _position++;
            if (unitStart == _position)
            {
                _position = whitespaceStart;
                return Quantity.Scalar(number);
            }
            var unit = Units.Resolve(text[unitStart.._position]);
            if (FirstUnit is null)
            {
                FirstUnit = unit.Symbol;
                FirstUnitHadSpace = unitStart > whitespaceStart;
            }
            return Quantity.Of(number * unit.ToBaseFactor, unit.Dimension);
        }

        private Quantity Apply(char op, Quantity left, Quantity right)
        {
            if (op is '+' or '-')
            {
                if (left.Dimension != right.Dimension)
                {
                    if (relative && selection is not null && left.Dimension != Dimension.Scalar && right.Dimension == Dimension.Scalar)
                        right = Quantity.Of(right.BaseValue * Units.Resolve(selection.Unit!).ToBaseFactor, left.Dimension);
                    else throw Error("Nie można dodawać wartości o różnych wymiarach");
                }
                return new Quantity(op == '+' ? left.BaseValue + right.BaseValue : left.BaseValue - right.BaseValue, left.Dimension);
            }
            if (op == '*')
            {
                if (left.Dimension != Dimension.Scalar && right.Dimension != Dimension.Scalar)
                    throw Error("Mnożenie dwóch wartości z jednostkami nie jest obsługiwane");
                return new Quantity(left.BaseValue * right.BaseValue,
                    left.Dimension != Dimension.Scalar ? left.Dimension : right.Dimension);
            }
            if (right.BaseValue == 0) throw Error("Dzielenie przez zero");
            if (left.Dimension == Dimension.Scalar && right.Dimension != Dimension.Scalar)
                throw Error("Dzielenie liczby przez wartość z jednostką nie jest obsługiwane");
            return new Quantity(left.BaseValue / right.BaseValue,
                left.Dimension == right.Dimension ? Dimension.Scalar : left.Dimension);
        }

        private static bool IsUnitCharacter(char c) => char.IsLetter(c) || c is 'µ' or 'μ' or 'Ω';
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
