using System.Globalization;

namespace QuickCalc.Core;

public sealed record CalculationResult(string Text, Quantity Value, bool IsRelative);

public sealed class ExpressionEvaluator
{
    public CalculationResult Evaluate(string expression, string? selectedText = null, bool convertToSi = false)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new CalculationException("Wpisz wyrażenie.");
        ParsedSelection.TryParse(selectedText, out var selection);
        var parserText = expression.Trim();
        var forcedFormat = ExtractResultFormat(ref parserText);
        if (parserText.Length == 0) throw new CalculationException("Wpisz wyrażenie przed suffixem formatu.");
        parserText = ExpandRelativeShortcut(parserText);

        var parser = new Parser(parserText, selection, selection?.Value);
        var value = parser.Parse();
        var relative = parser.UsesSelection;
        if (!relative && value.Dimension == Dimension.Scalar && parser.FirstUnit is null && selection?.Unit is not null)
        {
            var selectedUnit = Units.Resolve(selection.Unit);
            value = Quantity.Of(value.BaseValue * selectedUnit.ToBaseFactor, selectedUnit.Dimension);
        }
        EnsureFinite(value.BaseValue);

        string? outputUnit = null;
        if (value.Dimension != Dimension.Scalar)
        {
            outputUnit = convertToSi
                ? Units.ReadableSiUnit(value.BaseValue, value.Dimension)
                : MatchingOutputUnit(selection, parser, value.Dimension) ??
                  Units.ReadableSiUnit(value.BaseValue, value.Dimension);
        }

        var displayValue = outputUnit is null
            ? value.BaseValue
            : value.BaseValue / Units.Resolve(outputUnit).ToBaseFactor;
        var spaced = selection?.SpaceBeforeUnit ?? parser.FirstUnitHadSpace;
        var unitSuffix = outputUnit is null ? "" : (spaced ? " " : "") + outputUnit;
        var inheritedFormat = selection?.NumericFormat ?? parser.FirstNumberFormat ?? NumericFormat.Decimal;
        var outputFormat = forcedFormat ?? inheritedFormat;
        if (parser.ForcesDecimalResult) outputFormat = NumericFormat.Decimal;
        var formatted = value.Dimension == Dimension.Scalar
            ? FormatScalar(displayValue, outputFormat, value.BitWidth)
            : FormatDecimal(displayValue);
        return new CalculationResult(formatted + unitSuffix, value, relative);
    }

    private static string? MatchingOutputUnit(ParsedSelection? selection, Parser parser, Dimension dimension)
    {
        if (selection?.Unit is not null && Units.Resolve(selection.Unit).Dimension == dimension)
            return selection.Unit;
        if (parser.FirstUnit is not null && Units.Resolve(parser.FirstUnit).Dimension == dimension)
            return parser.FirstUnit;
        return null;
    }

    private static string ExpandRelativeShortcut(string text)
    {
        if (text[0] == '+' && StartsNumericOperand(text, 1)) return "x" + text;
        if (text[0] is '*' or '/') return "x" + text;
        if (text[0] == '-' && StartsNumericOperand(text, 1)) return "x" + text;
        if ((text[0] == 'p' && StartsUnaryOperand(text, 1)) || text[0] is '^' or '&' or '|' ||
            text.StartsWith("<<", StringComparison.Ordinal) ||
            text.StartsWith(">>", StringComparison.Ordinal)) return "x" + text;
        if (text == "!") return "!x";

        var functionLength = RelativeFunctionLength(text);
        if (functionLength == 0) return text;
        var position = functionLength;
        while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
        if (position == text.Length || text[position] is '+' or '-' or '*' or '/' or 'p' or '^' or '&' or '|')
            return text[..functionLength] + "x" + text[functionLength..];
        return text;
    }

    private static bool StartsNumericOperand(string text, int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
        return position < text.Length && (char.IsDigit(text[position]) || text[position] is '.' or ',');
    }

    private static bool StartsUnaryOperand(string text, int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
        return position < text.Length && (char.IsDigit(text[position]) ||
            text[position] is '.' or ',' or '(' or '+' or '-' or '!' or 'r' or '√' or 'x');
    }

    private static int RelativeFunctionLength(string text)
    {
        if (text.StartsWith("log", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("abs", StringComparison.OrdinalIgnoreCase)) return 3;
        if (text.StartsWith("u2", StringComparison.OrdinalIgnoreCase)) return 2;
        if (text.StartsWith("ln", StringComparison.OrdinalIgnoreCase)) return 2;
        return text[0] is 'r' or '√' ? 1 : 0;
    }

    private static NumericFormat? ExtractResultFormat(ref string text)
    {
        (string Token, NumericFormat Format)[] suffixes =
        [
            ("hex", NumericFormat.Hexadecimal), ("bin", NumericFormat.Binary),
            ("dec", NumericFormat.Decimal), ("h", NumericFormat.Hexadecimal),
            ("b", NumericFormat.Binary), ("d", NumericFormat.Decimal)
        ];

        foreach (var (token, format) in suffixes)
        {
            if (text.Equals(token, StringComparison.Ordinal))
            {
                text = "x";
                return format;
            }
            if (!text.EndsWith(token, StringComparison.Ordinal)) continue;
            var start = text.Length - token.Length;
            if (start <= 0 || !char.IsWhiteSpace(text[start - 1])) continue;
            text = text[..start].TrimEnd();
            return format;
        }

        if (IsCompleteHexLiteral(text)) return null;
        foreach (var (token, format) in suffixes)
        {
            if (!text.EndsWith(token, StringComparison.Ordinal) || text.Length == token.Length) continue;
            var candidate = text[..^token.Length].TrimEnd();
            if (candidate.Length == 0) continue;
            if (token.Length == 1 && char.IsLetter(candidate[^1])) continue;
            text = candidate;
            return format;
        }
        return null;
    }

    private static bool IsCompleteHexLiteral(string text)
    {
        var position = text.Length > 0 && text[0] is '+' or '-' ? 1 : 0;
        if (position + 2 >= text.Length || text[position] != '0' ||
            text[position + 1] is not ('x' or 'X')) return false;
        position += 2;
        var digitStart = position;
        while (position < text.Length && Uri.IsHexDigit(text[position])) position++;
        return position > digitStart && position == text.Length;
    }

    private static string FormatScalar(double value, NumericFormat format, int? bitWidth)
    {
        if (format == NumericFormat.Decimal || !TryGetInteger(value, out var integer))
            return FormatDecimal(value);
        var negative = integer < 0;
        var magnitude = negative ? (ulong)(-(integer + 1)) + 1 : (ulong)integer;
        var digits = format == NumericFormat.Hexadecimal
            ? magnitude.ToString("X", CultureInfo.InvariantCulture)
            : ToBinary(magnitude);
        if (!negative && bitWidth is > 0)
            digits = digits.PadLeft(format == NumericFormat.Hexadecimal
                ? (bitWidth.Value + 3) / 4 : bitWidth.Value, '0');
        return (negative ? "-" : "") + (format == NumericFormat.Hexadecimal ? "0x" : "0b") + digits;
    }

    private static string ToBinary(ulong value)
    {
        if (value == 0) return "0";
        Span<char> buffer = stackalloc char[64];
        var position = buffer.Length;
        while (value > 0)
        {
            buffer[--position] = (value & 1) == 0 ? '0' : '1';
            value >>= 1;
        }
        return new string(buffer[position..]);
    }

    private static string FormatDecimal(double value)
    {
        if (Math.Abs(value) < 5e-15) value = 0;
        return value.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    private static bool TryGetInteger(double value, out long integer)
    {
        integer = 0;
        if (!double.IsFinite(value) || value < long.MinValue || value > long.MaxValue || value != Math.Truncate(value))
            return false;
        integer = (long)value;
        return (double)integer == value;
    }

    private static void EnsureFinite(double value)
    {
        if (!double.IsFinite(value)) throw new CalculationException("Przepełnienie matematyczne.");
    }

    private sealed class Parser(string text, ParsedSelection? selection, Quantity? relativeSeed)
    {
        private int _position;
        public string? FirstUnit { get; private set; }
        public bool FirstUnitHadSpace { get; private set; }
        public NumericFormat? FirstNumberFormat { get; private set; }
        public bool UsesSelection { get; private set; }
        public bool ForcesDecimalResult { get; private set; }

        public Quantity Parse()
        {
            var value = ParseParallel();
            SkipWhite();
            if (_position != text.Length) throw Error("Nieobsługiwana składnia");
            return value;
        }

        private Quantity ParseParallel()
        {
            var value = ParseBitwiseOr();
            while (true)
            {
                SkipWhite();
                if (!Take("||")) return value;
                value = ApplyParallel(value, ParseBitwiseOr());
            }
        }

        private Quantity ParseBitwiseOr()
        {
            var value = ParseBitwiseXor();
            while (true)
            {
                SkipWhite();
                if (StartsWith("||", StringComparison.Ordinal)) return value;
                if (!Take('|')) return value;
                value = ApplyBitwise('|', value, ParseBitwiseXor());
            }
        }

        private Quantity ParseBitwiseXor()
        {
            var value = ParseBitwiseAnd();
            while (true)
            {
                SkipWhite();
                if (!Take('^')) return value;
                value = ApplyBitwise('^', value, ParseBitwiseAnd());
            }
        }

        private Quantity ParseBitwiseAnd()
        {
            var value = ParseShift();
            while (true)
            {
                SkipWhite();
                if (!Take('&')) return value;
                value = ApplyBitwise('&', value, ParseShift());
            }
        }

        private Quantity ParseShift()
        {
            var value = ParseAddSubtract();
            while (true)
            {
                SkipWhite();
                if (Take("<<")) value = ApplyShift(left: true, value, ParseAddSubtract());
                else if (Take(">>")) value = ApplyShift(left: false, value, ParseAddSubtract());
                else return value;
            }
        }

        private Quantity ParseAddSubtract()
        {
            var value = ParseMultiplyDivide();
            while (true)
            {
                SkipWhite();
                if (!Take('+') && !Take('-')) return value;
                var op = text[_position - 1];
                value = ApplyArithmetic(op, value, ParseMultiplyDivide());
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
                value = ApplyArithmetic(op, value, ParseUnary());
            }
        }

        private Quantity ParseUnary()
        {
            SkipWhite();
            if (Take('+')) return ParseUnary();
            if (Take('-')) { var value = ParseUnary(); return value with { BaseValue = -value.BaseValue }; }
            if (Take('!')) return ApplyNot(ParseUnary());
            if (TakeWord("u2")) return ParseTwosComplement();
            if (TakeWord("abs"))
            {
                var value = ParseUnary();
                return value with { BaseValue = Math.Abs(value.BaseValue) };
            }
            if (TakeWord("ln")) return ApplyLogarithm(ParseUnary(), natural: true);
            if (TakeWord("log")) return ApplyLogarithm(ParseUnary(), natural: false);
            if (Take('r') || Take('√'))
            {
                var value = ParseUnary();
                if (value.Dimension != Dimension.Scalar) throw Error("Pierwiastek z jednostki nie jest obsługiwany");
                if (value.BaseValue < 0) throw Error("Pierwiastek z liczby ujemnej");
                return Quantity.Scalar(Math.Sqrt(value.BaseValue));
            }
            return ParsePower();
        }

        private Quantity ApplyLogarithm(Quantity value, bool natural)
        {
            if (value.Dimension != Dimension.Scalar)
                throw Error("Logarytm z wartości z jednostką nie jest obsługiwany");
            if (value.BaseValue <= 0)
                throw Error("Logarytm wymaga liczby większej od zera");
            return Quantity.Scalar(natural ? Math.Log(value.BaseValue) : Math.Log10(value.BaseValue));
        }

        private Quantity ParseTwosComplement()
        {
            SkipWhite();
            Quantity value;
            if (Take('('))
            {
                SkipWhite();
                if (Take(')')) value = SelectedValue();
                else
                {
                    value = ParseParallel();
                    SkipWhite();
                    if (!Take(')')) throw Error("Brak zamykającego nawiasu funkcji u2");
                }
            }
            else value = ParseUnary();

            if (value.Dimension != Dimension.Scalar || value.BitWidth is null)
                throw Error("u2 wymaga wartości zapisanej w BIN albo HEX");
            var integer = RequireInteger(value);
            var width = value.BitWidth.Value;
            if (integer < 0 || width is < 1 or > 63)
                throw Error("u2 wymaga dodatniego wzorca BIN/HEX o szerokości 1–63 bitów");
            var raw = (ulong)integer;
            var limit = 1UL << width;
            if (raw >= limit) throw Error("Wartość nie mieści się w zapisanej szerokości bitowej");
            var sign = 1UL << (width - 1);
            var signed = (raw & sign) == 0 ? (long)raw : (long)(raw - limit);
            ForcesDecimalResult = true;
            return Quantity.Scalar(signed);
        }

        private Quantity ApplyNot(Quantity value)
        {
            var integer = RequireInteger(value);
            if (value.BitWidth is not int width) return Quantity.Scalar(~integer);
            if (integer < 0 || width is < 1 or > 63)
                throw Error("NOT dla BIN/HEX wymaga dodatniej wartości o szerokości 1–63 bitów");
            var mask = (1UL << width) - 1;
            var result = (~(ulong)integer) & mask;
            return Quantity.Scalar((long)result, width);
        }

        private Quantity ParsePower()
        {
            var value = ParsePrimary();
            SkipWhite();
            if (!Take('p')) return value;
            var exponent = ParseUnary();
            if (value.Dimension != Dimension.Scalar || exponent.Dimension != Dimension.Scalar)
                throw Error("Potęgowanie wartości z jednostką nie jest obsługiwane");
            return Quantity.Scalar(Math.Pow(value.BaseValue, exponent.BaseValue));
        }

        private Quantity ParsePrimary()
        {
            SkipWhite();
            if (Take('x'))
                return SelectedValue();
            if (TakeWord("pi") || Take('π')) return Quantity.Scalar(Math.PI);
            if (TakeWord("e")) return Quantity.Scalar(Math.E);
            if (Take('('))
            {
                var value = ParseParallel();
                SkipWhite();
                if (!Take(')')) throw Error("Brak zamykającego nawiasu");
                return value;
            }
            return ParseNumber();
        }

        private Quantity ParseNumber()
        {
            if (StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ParseBasedInteger(NumericFormat.Hexadecimal, 16, Uri.IsHexDigit);
            if (StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                return ParseBasedInteger(NumericFormat.Binary, 2, c => c is '0' or '1');

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
            FirstNumberFormat ??= NumericFormat.Decimal;

            var whitespaceStart = _position;
            SkipWhite();
            var unitStart = _position;
            if (IsPowerOperatorAt(unitStart))
            {
                _position = whitespaceStart;
                return Quantity.Scalar(number);
            }
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

        private Quantity ParseBasedInteger(NumericFormat format, int numberBase, Func<char, bool> isDigit)
        {
            _position += 2;
            var start = _position;
            while (_position < text.Length && isDigit(text[_position])) _position++;
            if (start == _position) throw Error(format == NumericFormat.Hexadecimal
                ? "Oczekiwano cyfr HEX" : "Oczekiwano cyfr BIN");
            var digits = text[start.._position];
            long number;
            try { number = Convert.ToInt64(digits, numberBase); }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
            {
                throw Error("Liczba całkowita jest poza obsługiwanym zakresem");
            }
            FirstNumberFormat ??= format;
            return Quantity.Scalar(number, checked(digits.Length * (numberBase == 2 ? 1 : 4)));
        }

        private bool IsPowerOperatorAt(int position)
        {
            if (position >= text.Length || text[position] != 'p') return false;
            position++;
            while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
            return position < text.Length && (char.IsDigit(text[position]) ||
                text[position] is '.' or ',' or 'x' or '(' or '+' or '-' or '!' or 'r' or '√');
        }

        private Quantity ApplyArithmetic(char op, Quantity left, Quantity right)
        {
            if (op is '+' or '-')
            {
                if (left.Dimension != right.Dimension)
                {
                    if (UsesSelection && selection?.Unit is not null &&
                        left.Dimension != Dimension.Scalar && right.Dimension == Dimension.Scalar)
                        right = Quantity.Of(right.BaseValue * Units.Resolve(selection.Unit).ToBaseFactor, left.Dimension);
                    else if (UsesSelection && selection?.Unit is not null &&
                             left.Dimension == Dimension.Scalar && right.Dimension != Dimension.Scalar)
                        left = Quantity.Of(left.BaseValue * Units.Resolve(selection.Unit).ToBaseFactor, right.Dimension);
                    else throw Error("Nie można dodawać wartości o różnych wymiarach");
                }
                return new Quantity(op == '+' ? left.BaseValue + right.BaseValue : left.BaseValue - right.BaseValue,
                    left.Dimension);
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
            {
                var reciprocalDimension = right.Dimension switch
                {
                    Dimension.Frequency => Dimension.Time,
                    Dimension.Time => Dimension.Frequency,
                    _ => throw Error("Dzielenie liczby przez wartość z tą jednostką nie jest obsługiwane")
                };
                return Quantity.Of(left.BaseValue / right.BaseValue, reciprocalDimension);
            }
            return new Quantity(left.BaseValue / right.BaseValue,
                left.Dimension == right.Dimension ? Dimension.Scalar : left.Dimension);
        }

        private Quantity ApplyBitwise(char op, Quantity left, Quantity right)
        {
            var leftInteger = RequireInteger(left);
            var rightInteger = RequireInteger(right);
            var result = op switch
            {
                '&' => leftInteger & rightInteger,
                '^' => leftInteger ^ rightInteger,
                '|' => leftInteger | rightInteger,
                _ => throw Error("Nieobsługiwany operator bitowy")
            };
            var width = Math.Max(left.BitWidth ?? 0, right.BitWidth ?? 0);
            return Quantity.Scalar(result, width == 0 ? null : width);
        }

        private Quantity ApplyParallel(Quantity left, Quantity right)
        {
            if (left.Dimension != right.Dimension)
            {
                var unit = selection?.Unit is not null && Units.Resolve(selection.Unit).Dimension == Dimension.Resistance
                    ? Units.Resolve(selection.Unit)
                    : FirstUnit is not null && Units.Resolve(FirstUnit).Dimension == Dimension.Resistance
                        ? Units.Resolve(FirstUnit)
                        : null;
                if (unit is not null && left.Dimension == Dimension.Resistance && right.Dimension == Dimension.Scalar)
                    right = Quantity.Of(right.BaseValue * unit.ToBaseFactor, Dimension.Resistance);
                else if (unit is not null && left.Dimension == Dimension.Scalar && right.Dimension == Dimension.Resistance)
                    left = Quantity.Of(left.BaseValue * unit.ToBaseFactor, Dimension.Resistance);
                else throw Error("Połączenie równoległe wymaga dwóch rezystancji albo dwóch skalarów");
            }
            if (left.Dimension is not (Dimension.Scalar or Dimension.Resistance))
                throw Error("Operator || obsługuje tylko rezystancje albo skalary");
            var denominator = left.BaseValue + right.BaseValue;
            if (denominator == 0) throw Error("Połączenie równoległe ma zerową sumę rezystancji");
            return Quantity.Of(left.BaseValue * right.BaseValue / denominator, left.Dimension);
        }

        private Quantity ApplyShift(bool left, Quantity value, Quantity countValue)
        {
            var integer = RequireInteger(value);
            var count = RequireInteger(countValue);
            if (count is < 0 or > 63) throw Error("Liczba przesunięć musi mieścić się w zakresie 0–63");
            return Quantity.Scalar(left ? integer << (int)count : integer >> (int)count, value.BitWidth);
        }

        private Quantity SelectedValue()
        {
            UsesSelection = true;
            return relativeSeed ?? throw Error("Symbol x wymaga zaznaczonej wartości liczbowej");
        }

        private long RequireInteger(Quantity value)
        {
            if (value.Dimension != Dimension.Scalar || !TryGetInteger(value.BaseValue, out var integer))
                throw Error("Operator bitowy wymaga integera bez jednostki");
            return integer;
        }

        private static bool IsUnitCharacter(char c) => char.IsLetter(c) || c is 'µ' or 'μ' or 'Ω';

        private bool Take(char expected)
        {
            if (_position >= text.Length || text[_position] != expected) return false;
            _position++;
            return true;
        }

        private bool Take(string expected)
        {
            if (!StartsWith(expected, StringComparison.Ordinal)) return false;
            _position += expected.Length;
            return true;
        }

        private bool TakeWord(string expected)
        {
            if (!StartsWith(expected, StringComparison.OrdinalIgnoreCase)) return false;
            _position += expected.Length;
            return true;
        }

        private bool StartsWith(string expected, StringComparison comparison) =>
            _position + expected.Length <= text.Length &&
            text.AsSpan(_position, expected.Length).Equals(expected.AsSpan(), comparison);

        private void SkipWhite()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++;
        }

        private CalculationException Error(string message) => new($"{message} (pozycja {_position + 1}).");
    }
}
