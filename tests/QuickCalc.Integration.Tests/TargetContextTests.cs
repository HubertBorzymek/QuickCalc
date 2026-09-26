using QuickCalc.Windows;
using QuickCalc.Core;
using System.Diagnostics;
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
    public void UiAutomationUsesNet9AssemblyAndDoesNotFallBackAfterLoadFailure()
    {
        StringAssert.Contains(TargetContext.UiAutomationAssemblyIdentity, "Version=9.0.0.0");
        Assert.AreEqual(Environment.Is64BitProcess ? 40 : 28, TargetContext.NativeInputSize);
        var target = TargetContext.CaptureForHandles(IntPtr.Zero, IntPtr.Zero);
        Assert.IsFalse(target.DetectionMethod.Contains("FileNotFoundException", StringComparison.Ordinal));
        Assert.IsFalse(target.DetectionMethod.Contains("TypeLoadException", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BasicCaptureUsedByClipboardModeDoesNotProbeSelection()
    {
        var timer = Stopwatch.StartNew();
        var target = TargetContext.CaptureBasic("test trybu schowka");
        timer.Stop();
        Assert.AreEqual("test trybu schowka", target.DetectionMethod);
        Assert.IsTrue(timer.Elapsed < TimeSpan.FromMilliseconds(100),
            $"Podstawowe przechwycenie trwało {timer.Elapsed.TotalMilliseconds:F0} ms.");
    }

    [TestMethod]
    public void AutomationCaptureHasBoundedLatency()
    {
        var timer = Stopwatch.StartNew();
        _ = TargetContext.CaptureForHandles(IntPtr.Zero, IntPtr.Zero);
        timer.Stop();
        Assert.IsTrue(timer.Elapsed < TimeSpan.FromMilliseconds(250),
            $"Przechwycenie UI Automation trwało {timer.Elapsed.TotalMilliseconds:F0} ms.");
    }

    [TestMethod]
    public void ClipboardProbeIsNeverUsedForAnUnselectedOrUnknownRange()
    {
        Assert.IsFalse(TargetContext.ShouldProbeClipboard(hasSelection: false,
            automationIndicatedSelection: false));
        Assert.IsFalse(TargetContext.ShouldProbeClipboard(hasSelection: true,
            automationIndicatedSelection: true));
        Assert.IsTrue(TargetContext.ShouldProbeClipboard(hasSelection: false,
            automationIndicatedSelection: true));
    }

    [TestMethod]
    public void KeyboardCopyFallbackAvoidsOnlyVisualStudioEditorCommand()
    {
        Assert.IsFalse(TargetContext.ShouldUseKeyboardCopyFallback("devenv"));
        Assert.IsFalse(TargetContext.ShouldUseKeyboardCopyFallback("DEVENV"));
        Assert.IsTrue(TargetContext.ShouldUseKeyboardCopyFallback("X2"));
        Assert.IsTrue(TargetContext.ShouldUseKeyboardCopyFallback("AltiumDesigner"));
        Assert.IsTrue(TargetContext.ShouldUseKeyboardCopyFallback(null));
    }

    [TestMethod]
    public void WindowMessageCopyReadsOnlyAnActualSelection()
    {
        RunStaWithFocusedTextBox("Value: 25mm", 7, 4, (form, box) =>
        {
            Assert.IsTrue(TargetContext.TryReadSelectionByWindowMessage(box.Handle, out var selected));
            Assert.AreEqual("25mm", selected);
            box.Select(7, 0);
            var timer = Stopwatch.StartNew();
            Assert.IsFalse(TargetContext.TryReadSelectionByWindowMessage(box.Handle, out _));
            timer.Stop();
            Assert.IsTrue(timer.Elapsed < TimeSpan.FromMilliseconds(300),
                $"WM_COPY bez zaznaczenia trwał {timer.Elapsed.TotalMilliseconds:F0} ms.");
        });
    }

    [TestMethod]
    public void KeyboardCopyFallbackReadsSelectionAndRejectsBareCaret()
    {
        RunStaWithFocusedTextBox("Height: 1mm", 8, 3, (form, box) =>
        {
            if (!TargetContext.TryReadSelectionByCopy(out var selected))
                throw new AssertInconclusiveException(
                    "Runner testów nie przyznał kontrolce pierwszego planu wymaganego przez Ctrl+C.");
            Assert.AreEqual("1mm", selected);
            box.Select(8, 0);
            Assert.IsFalse(TargetContext.TryReadSelectionByCopy(out _));
        });
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
                Assert.AreEqual("15mm", box.SelectedText);
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
            Assert.AreEqual("30mm", box.SelectedText,
                $"Kursor={box.SelectionStart}, długość={box.SelectionLength}, błąd={target.LastFailureReason ?? "brak"}");
        });
    }

    [TestMethod]
    public void RelativeCalculationSupportsSelectedNumberWithoutUnitEndToEnd()
    {
        RunStaWithFocusedTextBox("Value: 23", 7, 2, (form, box) =>
        {
            var target = TargetContext.CaptureForHandles(form.Handle, box.Handle);
            Assert.AreEqual("23", target.SelectedText);
            var result = new ExpressionEvaluator().Evaluate("+3", target.SelectedText);
            Assert.AreEqual("26", result.Text);
            Assert.IsTrue(target.InsertOrReplace(result.Text));
            Application.DoEvents();
            Assert.AreEqual("Value: 26", box.Text);
            Assert.AreEqual("26", box.SelectedText);
        });
    }

    [TestMethod]
    public void InsertionWithoutSelectionLeavesCaretAfterResult()
    {
        RunStaWithFocusedTextBox("Value: ", 7, 0, (form, box) =>
        {
            var target = TargetContext.CaptureForHandles(form.Handle, box.Handle);
            Assert.IsFalse(target.HasSelection);
            Assert.IsTrue(target.InsertOrReplace("1234"));
            Application.DoEvents();
            Assert.AreEqual("Value: 1234", box.Text);
            Assert.AreEqual(11, box.SelectionStart);
            Assert.AreEqual(0, box.SelectionLength);
        });
    }

    [TestMethod]
    public void VerifiedSelectionWithoutWin32IndexesUsesClipboardPasteFallback()
    {
        RunStaWithFocusedTextBox("Value: 25mm", 7, 4, (form, box) =>
        {
            var target = new TargetContext(form.Handle, box.Handle, null, null, "25mm", "symulacja UI Automation");
            if (!target.InsertOrReplace("30mm"))
                throw new AssertInconclusiveException("Runner testów nie otrzymał prawa do pierwszego planu wymaganego przez SendInput.");
            Application.DoEvents();
            Assert.AreEqual("Value: 30mm", box.Text);
            Assert.AreEqual("30mm", box.SelectedText,
                $"Kursor={box.SelectionStart}, długość={box.SelectionLength}, błąd={target.LastFailureReason ?? "brak"}");
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
