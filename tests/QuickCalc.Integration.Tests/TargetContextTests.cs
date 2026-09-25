using QuickCalc.Windows;
using System.Windows.Forms;

namespace QuickCalc.Integration.Tests;

[TestClass]
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
                var target = TargetContext.Capture();
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
}
