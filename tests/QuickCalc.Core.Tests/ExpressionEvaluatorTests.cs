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
    public void EvaluatesRelativeExpressions(string expression, string selection, string expected)
        => Assert.AreEqual(expected, _sut.Evaluate(expression, selection).Text);

    [TestMethod] public void AbsoluteScalarUsesSelectedUnit() => Assert.AreEqual("15mm", _sut.Evaluate("10+5", "25mm").Text);
    [TestMethod] public void NegativeWithoutSelectionIsAbsolute() => Assert.IsFalse(_sut.Evaluate("-5").IsRelative);
    [TestMethod] public void DividingLengthsProducesScalar() => Assert.AreEqual("5", _sut.Evaluate("/5mm", "25mm").Text);

    [TestMethod]
    [DataRow("+5")]
    [DataRow("*2")]
    [DataRow("/2")]
    public void RelativeOperationRequiresSelection(string expression)
        => Assert.ThrowsExactly<CalculationException>(() => _sut.Evaluate(expression));

    [TestMethod]
    [DataRow("1/0")]
    [DataRow("(2+3")]
    [DataRow("3mm*2mm")]
    [DataRow("3mm+2")]
    [DataRow("abc")]
    public void RejectsInvalidOrUnsupportedExpressions(string expression)
        => Assert.ThrowsExactly<CalculationException>(() => _sut.Evaluate(expression));

    [TestMethod]
    public void ParsesSelectedSpacingAndUnit()
    {
        Assert.IsTrue(ParsedSelection.TryParse(" 3 mm ", out var parsed));
        Assert.AreEqual("mm", parsed!.Unit); Assert.IsTrue(parsed.SpaceBeforeUnit);
    }
}
