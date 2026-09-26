using QuickCalc.Core;

namespace QuickCalc.Core.Tests;

[TestClass]
public sealed class ExpressionEvaluatorTests
{
    private readonly ExpressionEvaluator _sut = new();

    [TestMethod]
    [DataRow("2+3*4", null, "14")]
    [DataRow("(2+3)*(4-1)", null, "15")]
    [DataRow("-5", null, "-5")]
    [DataRow("0-5", "25mm", "-5mm")]
    [DataRow("3,5+1.5", null, "5")]
    [DataRow("1in+5mm", null, "1.19685039370079in")]
    [DataRow("3mm*2", null, "6mm")]
    [DataRow("10mm/2", null, "5mm")]
    [DataRow("10 mm/2", null, "5 mm")]
    [DataRow("2^3^2", null, "512")]
    [DataRow("r81", null, "9")]
    [DataRow("√(9+7)", null, "4")]
    [DataRow("4.7kΩ*2", null, "9.4kΩ")]
    [DataRow("3.3V+700mV", null, "4V")]
    [DataRow("10uF/2", null, "5µF")]
    [DataRow("250mA*2", null, "500mA")]
    [DataRow("20MHz/4", null, "5MHz")]
    [DataRow("3kPa+500Pa", null, "3.5kPa")]
    [DataRow("2kg/4", null, "0.5kg")]
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
    [DataRow("^2", "32", "1024")]
    [DataRow("^2+3", "32", "1027")]
    [DataRow("r", "81", "9")]
    [DataRow("r+7", "81", "16")]
    [DataRow("r*2+1", "81", "19")]
    [DataRow("√/3", "81", "3")]
    public void EvaluatesRelativeExpressions(string expression, string selection, string expected)
        => Assert.AreEqual(expected, _sut.Evaluate(expression, selection).Text);

    [TestMethod] public void AbsoluteScalarUsesSelectedUnit() => Assert.AreEqual("15mm", _sut.Evaluate("10+5", "25mm").Text);
    [TestMethod] public void AbsoluteScalarReplacesNegativeSelectionAndKeepsItsUnit() =>
        Assert.AreEqual("10mm", _sut.Evaluate("10", "-18.3mm").Text);
    [TestMethod] public void NegativeWithoutSelectionIsAbsolute() => Assert.IsFalse(_sut.Evaluate("-5").IsRelative);
    [TestMethod] public void DividingLengthsProducesScalar() => Assert.AreEqual("5", _sut.Evaluate("/5mm", "25mm").Text);
    [TestMethod] public void ConvertsSmallLengthToReadableSi() => Assert.AreEqual("0.04mm", _sut.Evaluate("/500", "2cm", true).Text);
    [TestMethod] public void ConvertsImperialLengthToSi() => Assert.AreEqual("1.524m", _sut.Evaluate("*1", "5ft", true).Text);
    [TestMethod] public void ConversionPreservesSelectedSpacing() => Assert.AreEqual("1.524 m", _sut.Evaluate("*1", "5 ft", true).Text);
    [TestMethod] public void ConvertsElectronicsUnitToEngineeringPrefix() => Assert.AreEqual("4.7kΩ", _sut.Evaluate("*1", "4700Ω", true).Text);

    [TestMethod]
    [DataRow("+5")]
    [DataRow("*2")]
    [DataRow("/2")]
    [DataRow("^2")]
    [DataRow("r")]
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
    public void RejectsInvalidOrUnsupportedExpressions(string expression)
        => Assert.ThrowsExactly<CalculationException>(() => _sut.Evaluate(expression));

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
}
