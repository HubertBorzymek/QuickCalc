using QuickCalc.App;
using QuickCalc.Core;
using QuickCalc.Windows;
using System.Windows.Forms;

namespace QuickCalc.Integration.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CalculatorPopupTests
{
    [TestMethod]
    public void PopupCanBeShownAndDisplaysLiveResult()
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var popup = new CalculatorPopup(CalculatorMode.Clipboard,
                    new TargetContext(IntPtr.Zero, IntPtr.Zero, null, null, null, "test"),
                    new ExpressionEvaluator(), new ExpressionHistory());
                popup.OperationFinished += (_, _) => { };
                popup.Show(); Application.DoEvents();
                Assert.IsTrue(popup.Visible);
                Assert.AreNotEqual(IntPtr.Zero, popup.Handle);
                var input = popup.Controls.OfType<TextBox>().Single();
                input.Text = "10+5"; Application.DoEvents();
                Assert.IsTrue(popup.Controls.OfType<Label>().Any(label => label.Text == "= 15"));
                popup.Close(); popup.Close();
            }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(10)), "Test popupu przekroczył limit czasu.");
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void ShiftEnterSubmissionCanConvertImperialSelectionToSi()
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var popup = new CalculatorPopup(CalculatorMode.Context,
                    new TargetContext((IntPtr)1, (IntPtr)1, null, null, "5ft", "test"),
                    new ExpressionEvaluator(), new ExpressionHistory());
                PopupOperation? completed = null;
                popup.OperationFinished += (_, operation) => completed = operation;
                popup.Controls.OfType<TextBox>().Single().Text = "*1";
                popup.Submit(convertToSi: true);
                Assert.IsNotNull(completed);
                Assert.AreEqual("1.524m", completed.Result);
            }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }

    [STATestMethod]
    public void ShiftEnterOnScalarKeepsNormalUnitlessResult()
    {
        using var popup = new CalculatorPopup(CalculatorMode.Clipboard,
            new TargetContext(IntPtr.Zero, IntPtr.Zero, null, null, null, "test"),
            new ExpressionEvaluator(), new ExpressionHistory());
        PopupOperation? completed = null;
        popup.OperationFinished += (_, operation) => completed = operation;
        popup.Controls.OfType<TextBox>().Single().Text = "5+3";

        popup.Submit(convertToSi: true);

        Assert.IsNotNull(completed);
        Assert.AreEqual("8", completed.Result);
    }

    [STATestMethod]
    public void EscapeCancelsWithoutReturningAResult()
    {
        using var popup = new CalculatorPopup(CalculatorMode.Context,
            new TargetContext((IntPtr)1, (IntPtr)1, null, null, "25mm", "test"),
            new ExpressionEvaluator(), new ExpressionHistory());
        PopupOperation? completed = null;
        popup.OperationFinished += (_, operation) => completed = operation;

        Assert.IsTrue(popup.HandleCommand(Keys.Escape));
        Assert.IsNotNull(completed);
        Assert.IsTrue(completed.Cancelled);
        Assert.IsNull(completed.Result);
    }

    [STATestMethod]
    public void EnterCommandSubmitsNormalResult()
    {
        using var popup = new CalculatorPopup(CalculatorMode.Context,
            new TargetContext((IntPtr)1, (IntPtr)1, null, null, "25mm", "test"),
            new ExpressionEvaluator(), new ExpressionHistory());
        PopupOperation? completed = null;
        popup.OperationFinished += (_, operation) => completed = operation;
        popup.Controls.OfType<TextBox>().Single().Text = "+5";

        Assert.IsTrue(popup.HandleCommand(Keys.Enter));
        Assert.IsNotNull(completed);
        Assert.IsFalse(completed.Cancelled);
        Assert.AreEqual("30mm", completed.Result);
    }

    [TestMethod]
    public void SuccessfulSubmissionCanClosePopupImmediately()
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var popup = new CalculatorPopup(CalculatorMode.Clipboard,
                    new TargetContext(IntPtr.Zero, IntPtr.Zero, null, null, null, "test"),
                    new ExpressionEvaluator(), new ExpressionHistory());
                popup.OperationFinished += (_, _) => { popup.Hide(); popup.Close(); };
                popup.Show();
                popup.Controls.OfType<TextBox>().Single().Text = "2+3";
                popup.Submit(convertToSi: false);
                Application.DoEvents();
                Assert.IsTrue(popup.IsDisposed || !popup.Visible);
            }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void CompactPopupIsBorderlessAndTogglePreservesExpressionAndFocus()
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var popup = new CalculatorPopup(CalculatorMode.Context,
                    new TargetContext((IntPtr)1, (IntPtr)1, null, null, "-60", "test"),
                    new ExpressionEvaluator(), new ExpressionHistory());
                popup.Show(); Application.DoEvents();
                var input = popup.Controls.OfType<TextBox>().Single();
                input.Text = "+10";
                input.Focus();
                Application.DoEvents();

                Assert.AreEqual(FormBorderStyle.None, popup.FormBorderStyle);
                Assert.IsFalse(popup.ShowInTaskbar);
                Assert.IsTrue(popup.TopMost);
                Assert.IsTrue(popup.Width is >= 320 and <= 500);
                Assert.IsTrue(popup.Height is >= 52 and <= 90);
                Assert.AreEqual(0.91, popup.Opacity, 0.001);
                Assert.IsTrue(popup.Controls.OfType<Label>().Any(label => label.Text == "= -50"));

                var compactHeight = popup.Height;
                popup.ToggleExpanded(); Application.DoEvents();
                Assert.IsTrue(popup.IsExpanded);
                Assert.IsGreaterThan(compactHeight, popup.Height);
                Assert.AreEqual("+10", input.Text);
                Assert.IsTrue(input.Focused);
                Assert.IsTrue(popup.Controls.OfType<Label>().Any(label => label.Text.Contains("Shift+Enter — SI")));

                popup.ToggleExpanded(); Application.DoEvents();
                Assert.IsFalse(popup.IsExpanded);
                Assert.AreEqual(compactHeight, popup.Height);
                Assert.AreEqual("+10", input.Text);
                popup.Close(); popup.Close();
            }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void ParserErrorIsShownInlineAndTemporarilyGrowsCompactPopup()
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var popup = new CalculatorPopup(CalculatorMode.Clipboard,
                    new TargetContext(IntPtr.Zero, IntPtr.Zero, null, null, null, "test"),
                    new ExpressionEvaluator(), new ExpressionHistory());
                popup.Show(); Application.DoEvents();
                var compactHeight = popup.Height;
                popup.Controls.OfType<TextBox>().Single().Text = "1+";
                Application.DoEvents();

                Assert.IsGreaterThan(compactHeight, popup.Height);
                Assert.IsTrue(popup.Controls.OfType<Label>().Any(label =>
                    label.Visible && label.Text.Contains("Oczekiwano liczby")));
                popup.Close(); popup.Close();
            }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void ContextPopupAppearsBelowTargetFieldWhenSpaceAllows()
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                var area = Screen.PrimaryScreen!.WorkingArea;
                var targetBounds = new Rectangle(area.Left + 100, area.Top + 100, 280, 30);
                using var popup = new CalculatorPopup(CalculatorMode.Context,
                    new TargetContext((IntPtr)1, (IntPtr)1, null, null, null, "test", targetBounds),
                    new ExpressionEvaluator(), new ExpressionHistory());
                popup.Show(); popup.MoveToPreferredLocation(); Application.DoEvents();
                Assert.AreEqual(targetBounds.Left, popup.Left);
                var gap = popup.Top - targetBounds.Bottom;
                Assert.IsTrue(gap is >= 6 and <= 16, $"Nieoczekiwany odstęp popupu: {gap}px.");
                Assert.IsFalse(popup.Bounds.IntersectsWith(targetBounds));
                popup.Close(); popup.Close();
            }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void ClipboardPopupAppearsNearCursorAndInsideItsMonitor()
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                var cursor = Cursor.Position;
                var area = Screen.FromPoint(cursor).WorkingArea;
                using var popup = new CalculatorPopup(CalculatorMode.Clipboard,
                    new TargetContext(IntPtr.Zero, IntPtr.Zero, null, null, null, "test"),
                    new ExpressionEvaluator(), new ExpressionHistory());
                popup.Show(); popup.MoveToPreferredLocation(); Application.DoEvents();

                Assert.IsTrue(area.Contains(popup.Bounds),
                    $"Popup {popup.Bounds} wyszedł poza monitor {area}.");
                Assert.IsLessThanOrEqualTo(popup.Width + 40, Math.Abs(popup.Left - cursor.X));
                Assert.IsLessThanOrEqualTo(popup.Height + 40, Math.Abs(popup.Top - cursor.Y));
                popup.Close(); popup.Close();
            }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }
}
