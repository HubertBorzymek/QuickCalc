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
                Assert.AreEqual(targetBounds.Bottom + 10, popup.Top);
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
}
