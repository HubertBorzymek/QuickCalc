using QuickCalc.Core;

namespace QuickCalc.Core.Tests;

[TestClass]
public sealed class ExpressionHistoryTests
{
    [TestMethod]
    public void NavigatesAndAvoidsDuplicates()
    {
        var history = new ExpressionHistory(3);
        history.Add("+2.5"); history.Add("10+5"); history.Add("+2.5");
        Assert.HasCount(2, history.Items);
        Assert.AreEqual("+2.5", history.Previous()); Assert.AreEqual("10+5", history.Previous());
        Assert.AreEqual("+2.5", history.Next()); Assert.AreEqual("", history.Next());
    }

    [TestMethod]
    public void LimitsHistorySize()
    {
        var history = new ExpressionHistory(2); history.Add("1"); history.Add("2"); history.Add("3");
        CollectionAssert.AreEqual(new[] { "2", "3" }, history.Items.ToArray());
    }

    [TestMethod]
    public void RelativeExpressionUsesCurrentSelection()
    {
        var evaluator = new ExpressionEvaluator();
        Assert.AreEqual("12.5mm", evaluator.Evaluate("+2.5", "10mm").Text);
        Assert.AreEqual("22.5mm", evaluator.Evaluate("+2.5", "20mm").Text);
    }
}
