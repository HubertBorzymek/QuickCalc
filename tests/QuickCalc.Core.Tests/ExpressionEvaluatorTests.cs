using QuickCalc.Core;

namespace QuickCalc.Core.Tests;

[TestClass]
public sealed class ExpressionEvaluatorTests
{
    private readonly ExpressionEvaluator _sut = new();

    [TestMethod]
    [DataRow("2+3*4", null, "14")]
    [DataRow("(2+3)*(4-1)", null, "15")]
    [DataRow("(-5)", null, "-5")]
    [DataRow("0-5", "25mm", "-5mm")]
    [DataRow("3,5+1.5", null, "5")]
    [DataRow("1in+5mm", null, "1.19685039370079in")]
    [DataRow("3mm*2", null, "6mm")]
    [DataRow("10mm/2", null, "5mm")]
    [DataRow("10 mm/2", null, "5 mm")]
    [DataRow("2p3p2", null, "512")]
    [DataRow("r81", null, "9")]
    [DataRow("√(9+7)", null, "4")]
    [DataRow("ln(e)", null, "1")]
    [DataRow("ln e", null, "1")]
    [DataRow("log1000", null, "3")]
    [DataRow("log(100)", null, "2")]
    [DataRow("pi", null, "3.14159265358979")]
    [DataRow("π", null, "3.14159265358979")]
    [DataRow("e", null, "2.71828182845905")]
    [DataRow("2*pi", null, "6.28318530717959")]
    [DataRow("4.7kΩ*2", null, "9.4kΩ")]
    [DataRow("3.3V+700mV", null, "4V")]
    [DataRow("10uF/2", null, "5µF")]
    [DataRow("250mA*2", null, "500mA")]
    [DataRow("20MHz/4", null, "5MHz")]
    [DataRow("3kPa+500Pa", null, "3.5kPa")]
    [DataRow("2kg/4", null, "0.5kg")]
    [DataRow("10pF*2", null, "20pF")]
    public void EvaluatesAbsoluteExpressions(string expression, string? selection, string expected)
        => Assert.AreEqual(expected, _sut.Evaluate(expression, selection).Text);

    [TestMethod]
    [DataRow("+5", "25mm", "30mm")]
    [DataRow("-5", "25mm", "20mm")]
    [DataRow("*2", "25mm", "50mm")]
    [DataRow("/2", "25mm", "12.5mm")]
    [DataRow("+2.5*3", "25mm", "32.5mm")]
    [DataRow("*(2+3)", "25mm", "125mm")]
    [DataRow("/2+5", "25mm", "17.5mm")]
    [DataRow("+5mil", "3mm", "3.127mm")]
    [DataRow("+1mm", "100mil", "139.370078740157mil")]
    [DataRow("+5", "3 mm", "8 mm")]
    [DataRow("+3", "23", "26")]
    [DataRow("*2", "12.5", "25")]
    [DataRow("xp2", "32", "1024")]
    [DataRow("xp2+3", "32", "1027")]
    [DataRow("-x", "32", "-32")]
    [DataRow("1/x", "4", "0.25")]
    [DataRow("rx", "81", "9")]
    [DataRow("lnx", "1", "0")]
    [DataRow("(x+5)/2", "25", "15")]
    [DataRow("+x", "25", "25")]
    [DataRow("r", "81", "9")]
    [DataRow("r+7", "81", "16")]
    [DataRow("r*2+1", "81", "19")]
    [DataRow("√/3", "81", "3")]
    [DataRow("log", "100", "2")]
    [DataRow("log*3", "100", "6")]
    [DataRow("ln", "1", "0")]
    [DataRow("ln+2", "1", "2")]
    [DataRow("ln e", "100", "1")]
    [DataRow("log 1000", "25", "3")]
    public void EvaluatesRelativeExpressions(string expression, string selection, string expected)
        => Assert.AreEqual(expected, _sut.Evaluate(expression, selection).Text);

    [TestMethod] public void AbsoluteScalarUsesSelectedUnit() => Assert.AreEqual("15mm", _sut.Evaluate("10+5", "25mm").Text);
    [TestMethod] public void AbsoluteScalarReplacesNegativeSelectionAndKeepsItsUnit() =>
        Assert.AreEqual("10mm", _sut.Evaluate("10", "-18.3mm").Text);
    [TestMethod] public void ParenthesizedNegativeWithoutSelectionIsAbsolute() => Assert.IsFalse(_sut.Evaluate("(-5)").IsRelative);
    [TestMethod] public void DividingLengthsProducesScalar() => Assert.AreEqual("5", _sut.Evaluate("/5mm", "25mm").Text);
    [TestMethod] public void ConvertsSmallLengthToReadableSi() => Assert.AreEqual("0.04mm", _sut.Evaluate("/500", "2cm", true).Text);
    [TestMethod] public void ConvertsImperialLengthToSi() => Assert.AreEqual("1.524m", _sut.Evaluate("*1", "5ft", true).Text);
    [TestMethod] public void ConversionPreservesSelectedSpacing() => Assert.AreEqual("1.524 m", _sut.Evaluate("*1", "5 ft", true).Text);
    [TestMethod] public void ConvertsElectronicsUnitToEngineeringPrefix() => Assert.AreEqual("4.7kΩ", _sut.Evaluate("*1", "4700Ω", true).Text);

    [TestMethod]
    [DataRow("+5")]
    [DataRow("*2")]
    [DataRow("/2")]
    [DataRow("-5")]
    [DataRow("r")]
    [DataRow("ln")]
    [DataRow("log")]
    [DataRow("x")]
    [DataRow("-x")]
    [DataRow("1/x")]
    [DataRow("rx")]
    [DataRow("lnx")]
    [DataRow("(x+5)/2")]
    public void RelativeOperationRequiresSelection(string expression)
        => Assert.ThrowsExactly<CalculationException>(() => _sut.Evaluate(expression));

    [TestMethod]
    [DataRow("1/0")]
    [DataRow("(2+3")]
    [DataRow("3mm*2mm")]
    [DataRow("3mm+2")]
    [DataRow("abc")]
    [DataRow("r-1")]
    [DataRow("2mm^2")]
    [DataRow("ln(0)")]
    [DataRow("ln(-1)")]
    [DataRow("log(0)")]
    [DataRow("log(-10)")]
    [DataRow("ln(2mm)")]
    public void RejectsInvalidOrUnsupportedExpressions(string expression)
        => Assert.ThrowsExactly<CalculationException>(() => _sut.Evaluate(expression));

    [TestMethod]
    [DataRow("2p8", "256")]
    [DataRow("2^3", "1")]
    [DataRow("6&3", "2")]
    [DataRow("4|1", "5")]
    [DataRow("7^3", "4")]
    [DataRow("~5", "-6")]
    [DataRow("1<<4", "16")]
    [DataRow("16>>2", "4")]
    [DataRow("1|2&4", "1")]
    [DataRow("1+2<<2", "12")]
    public void EvaluatesPowerAndBitwiseOperators(string expression, string expected)
        => Assert.AreEqual(expected, _sut.Evaluate(expression).Text);

    [TestMethod]
    [DataRow("1.5&1")]
    [DataRow("1&2.5")]
    [DataRow("2mm&1")]
    [DataRow("1<<-1")]
    [DataRow("1<<64")]
    public void BitwiseOperatorsRejectNonIntegersUnitsAndInvalidShiftCounts(string expression)
        => Assert.ThrowsExactly<CalculationException>(() => _sut.Evaluate(expression));

    [TestMethod]
    [DataRow("10+0x10", "26")]
    [DataRow("0x10+10", "0x1A")]
    [DataRow("0b1000+0x10", "0b11000")]
    [DataRow("0xFF-0b1", "0xFE")]
    [DataRow("0x5/2", "2.5")]
    public void MixesDecimalHexAndBinaryAndInheritsFirstLiteralFormat(string expression, string expected)
        => Assert.AreEqual(expected, _sut.Evaluate(expression).Text);

    [TestMethod]
    [DataRow("x+1", "0xD", "0xE")]
    [DataRow("x+1", "0b1101", "0b1110")]
    [DataRow("10+1", "0xD", "0xB")]
    [DataRow("x/2", "0x5", "2.5")]
    public void SelectedNumberFormatHasPriorityAndFloatResultIsDecimal(
        string expression, string selection, string expected)
        => Assert.AreEqual(expected, _sut.Evaluate(expression, selection).Text);

    [TestMethod]
    [DataRow("13h", "0xD")]
    [DataRow("13 h", "0xD")]
    [DataRow("13hex", "0xD")]
    [DataRow("13 hex", "0xD")]
    [DataRow("13b", "0b1101")]
    [DataRow("13 b", "0b1101")]
    [DataRow("13bin", "0b1101")]
    [DataRow("13 bin", "0b1101")]
    [DataRow("13d", "13")]
    [DataRow("13 d", "13")]
    [DataRow("13dec", "13")]
    [DataRow("13 dec", "13")]
    [DataRow("10+0x20h", "0x2A")]
    [DataRow("0x20+10 d", "42")]
    [DataRow("0x5/2h", "2.5")]
    public void SupportsForcedResultFormatSuffixes(string expression, string expected)
        => Assert.AreEqual(expected, _sut.Evaluate(expression).Text);

    [TestMethod]
    [DataRow("0xABCD", "0xABCD")]
    [DataRow("0xABCDb", "0xABCDB")]
    [DataRow("0xABCDd", "0xABCDD")]
    [DataRow("0xABCDdec", "0xABCDDEC")]
    [DataRow("0xABCD b", "0b1010101111001101")]
    [DataRow("0xABCD d", "43981")]
    [DataRow("0xABCD dec", "43981")]
    public void CompleteHexLiteralWinsOverUnspacedSuffix(string expression, string expected)
        => Assert.AreEqual(expected, _sut.Evaluate(expression).Text);

    [TestMethod]
    public void ReciprocalFrequencyConvertsToReadableSiTime()
        => Assert.AreEqual("400 µs", _sut.Evaluate("1/x", "2.5 kHz", true).Text);

    [TestMethod]
    public void ParsesSelectedSpacingAndUnit()
    {
        Assert.IsTrue(ParsedSelection.TryParse(" 3 mm ", out var parsed));
        Assert.AreEqual("mm", parsed!.Unit); Assert.IsTrue(parsed.SpaceBeforeUnit);
    }

    [TestMethod]
    public void ParsesSelectedNumberWithoutUnit()
    {
        Assert.IsTrue(ParsedSelection.TryParse("23", out var parsed));
        Assert.IsNull(parsed!.Unit);
        Assert.AreEqual(Dimension.Scalar, parsed.Value.Dimension);
    }

    [TestMethod]
    [DataRow("0xD", 13d, NumericFormat.Hexadecimal)]
    [DataRow("0b1101", 13d, NumericFormat.Binary)]
    public void ParsesSelectedBasedInteger(string text, double expected, NumericFormat expectedFormat)
    {
        Assert.IsTrue(ParsedSelection.TryParse(text, out var parsed));
        Assert.AreEqual(expected, parsed!.Value.BaseValue);
        Assert.AreEqual(expectedFormat, parsed.NumericFormat);
    }
}
