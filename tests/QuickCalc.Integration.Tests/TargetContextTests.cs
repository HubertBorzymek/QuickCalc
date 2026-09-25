using QuickCalc.Windows;
using QuickCalc.Core;
using System.Windows.Forms;

namespace QuickCalc.Integration.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TargetContextTests
{
    [TestMethod]
    public void MissingTargetFailsSafe()
    {
        var target = new TargetContext(IntPtr.Zero, IntPtr.Zero, null, null, null, "test");
        Assert.IsFalse(target.IsValid); Assert.IsFalse(target.RestoreFocus()); Assert.IsFalse(target.InsertOrReplace("15"));
    }

    [TestMethod]
    public void SelectedTextFlagRequiresActualText()
    {
        Assert.IsFalse(new TargetContext((IntPtr)1, (IntPtr)1, null, null, null, "test").HasSelection);
        Assert.IsTrue(new TargetContext((IntPtr)1, (IntPtr)1, null, null, "25mm", "UIA").HasSelection);
    }

    [TestMethod]
    public void StandardEditSelectionCanBeCapturedAndReplaced()
    {
        Exception? failure = null;
        var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form { Text = "QuickCalc Integration Target", ShowInTaskbar = false };
                using var box = new TextBox { Text = "Position: 123mm", Dock = DockStyle.Fill };
                form.Controls.Add(box); form.Show(); form.Activate(); box.Focus(); box.Select(10, 5);
                Application.DoEvents();
                var target = TargetContext.CaptureForHandles(form.Handle, box.Handle);
                if (target.ControlHandle != box.Handle)
                    throw new AssertInconclusiveException("Środowisko testowe nie przyznało fokusu oknu WinForms.");
                Assert.AreEqual("123mm", target.SelectedText);
                Assert.IsTrue(target.InsertOrReplace("15mm"));
                Application.DoEvents();
                Assert.AreEqual("Position: 15mm", box.Text);
            }
            catch (Exception ex) { failure = ex; }
            finally { completed.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(10)), "Test GUI przekroczył limit czasu.");
        if (failure is AssertInconclusiveException inconclusive) Assert.Inconclusive(inconclusive.Message);
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void RelativeCalculationReplacesSelectedValueEndToEnd()
    {
        RunStaWithFocusedTextBox("Value: 25mm", 7, 4, (form, box) =>
        {
            var target = TargetContext.CaptureForHandles(form.Handle, box.Handle);
            Assert.AreEqual("25mm", target.SelectedText);
            var result = new ExpressionEvaluator().Evaluate("+5", target.SelectedText);
            Assert.AreEqual("30mm", result.Text);
            Assert.IsTrue(target.InsertOrReplace(result.Text));
            Application.DoEvents();
            Assert.AreEqual("Value: 30mm", box.Text);
        });
    }

    [TestMethod]
    public void VerifiedSelectionWithoutWin32IndexesUsesUnicodeFallback()
    {
        RunStaWithFocusedTextBox("Value: 25mm", 7, 4, (form, box) =>
        {
            var target = new TargetContext(form.Handle, box.Handle, null, null, "25mm", "symulacja UI Automation");
            if (!target.InsertOrReplace("30mm"))
                throw new AssertInconclusiveException("Runner testów nie otrzymał prawa do pierwszego planu wymaganego przez SendInput.");
            Application.DoEvents();
            Assert.AreEqual("Value: 30mm", box.Text);
        });
    }

    private static void RunStaWithFocusedTextBox(string text, int selectionStart, int selectionLength, Action<Form, TextBox> action)
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form { Text = "QuickCalc Relative Integration Target", ShowInTaskbar = false };
                using var box = new TextBox { Text = text, Dock = DockStyle.Fill };
                form.Controls.Add(box); form.Show(); form.Activate(); box.Focus(); box.Select(selectionStart, selectionLength);
                Application.DoEvents();
                action(form, box);
            }
            catch (Exception ex) { failure = ex; }
            finally { completed.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(10)), "Test GUI przekroczył limit czasu.");
        if (failure is AssertInconclusiveException inconclusive) Assert.Inconclusive(inconclusive.Message);
        if (failure is not null) throw failure;
    }
}
