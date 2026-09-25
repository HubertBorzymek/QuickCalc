using QuickCalc.Windows;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickCalc.Integration.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GlobalHotkeyWindowTests
{
    private const int WmHotkey = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [TestMethod]
    public void WmHotkeyRaisesPressedEvent()
    {
        RunSta(() =>
        {
            using var window = new GlobalHotkeyWindow();
            var received = 0;
            window.Pressed += (_, id) => received = id;
            Assert.IsTrue(PostMessage(window.Handle, WmHotkey, (IntPtr)77, IntPtr.Zero));
            Application.DoEvents();
            Assert.AreEqual(77, received);
        });
    }

    [TestMethod]
    public void FailedSecondRegistrationDoesNotRemoveFirstRegistration()
    {
        RunSta(() =>
        {
            using var first = new GlobalHotkeyWindow();
            using var second = new GlobalHotkeyWindow();
            first.Register(9001, Keys.F24, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift);
            Assert.ThrowsExactly<Win32Exception>(() =>
                second.Register(9002, Keys.F24, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift));
            Assert.IsTrue(first.IsRegistered(9001));
            Assert.IsFalse(second.IsRegistered(9002));
        });
    }

    [TestMethod]
    public void UnregisterAllowsRuntimeShortcutChange()
    {
        RunSta(() =>
        {
            using var first = new GlobalHotkeyWindow();
            using var second = new GlobalHotkeyWindow();
            first.Register(9011, Keys.F23, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift);
            first.Unregister(9011);
            Assert.IsFalse(first.IsRegistered(9011));
            second.Register(9012, Keys.F23, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift);
            Assert.IsTrue(second.IsRegistered(9012));
        });
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(10)), "Test skrótu przekroczył limit czasu.");
        if (failure is not null) throw failure;
    }
}
