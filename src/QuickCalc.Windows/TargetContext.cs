using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace QuickCalc.Windows;

public sealed record TargetContext(
    IntPtr WindowHandle,
    IntPtr ControlHandle,
    int? SelectionStart,
    int? SelectionEnd,
    string? SelectedText,
    string DetectionMethod)
{
    public bool HasSelection => !string.IsNullOrEmpty(SelectedText);

    public static TargetContext Capture()
    {
        var window = NativeMethods.GetForegroundWindow();
        var thread = NativeMethods.GetWindowThreadProcessId(window, out _);
        var info = new NativeMethods.GuiThreadInfo { cbSize = Marshal.SizeOf<NativeMethods.GuiThreadInfo>() };
        var control = NativeMethods.GetGUIThreadInfo(thread, ref info) && info.hwndFocus != IntPtr.Zero ? info.hwndFocus : window;

        if (TryStandardEditSelection(control, out var start, out var end, out var text))
            return new TargetContext(window, control, start, end, text, "Win32 EM_GETSEL");
        if (TryAutomationSelection(control, out text))
            return new TargetContext(window, control, null, null, text, "UI Automation TextPattern");
        return new TargetContext(window, control, null, null, null, "brak wiarygodnej detekcji");
    }

    public bool IsValid => WindowHandle != IntPtr.Zero && ControlHandle != IntPtr.Zero &&
                           NativeMethods.IsWindow(WindowHandle) && NativeMethods.IsWindow(ControlHandle);

    public bool RestoreFocus()
    {
        if (!IsValid || !NativeMethods.IsWindowEnabled(WindowHandle)) return false;
        var targetThread = NativeMethods.GetWindowThreadProcessId(WindowHandle, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();
        var attached = targetThread != currentThread && NativeMethods.AttachThreadInput(currentThread, targetThread, true);
        try
        {
            if (!NativeMethods.SetForegroundWindow(WindowHandle)) return false;
            NativeMethods.SetFocus(ControlHandle);
            if (SelectionStart is int start && SelectionEnd is int end)
                NativeMethods.SendMessage(ControlHandle, NativeMethods.EmSetSel, (IntPtr)start, (IntPtr)end);
            return true;
        }
        finally { if (attached) NativeMethods.AttachThreadInput(currentThread, targetThread, false); }
    }

    public bool InsertOrReplace(string value)
    {
        if (!RestoreFocus()) return false;
        // TextPattern can prove that text was selected, but cannot restore or replace that
        // range universally. Refuse instead of risking insertion into the wrong position.
        if (HasSelection && !SelectionStart.HasValue) return false;
        if (SelectionStart.HasValue)
        {
            NativeMethods.SendMessage(ControlHandle, NativeMethods.EmReplaceSel, (IntPtr)1, value);
            return true;
        }
        return SendUnicode(value);
    }

    private static bool TryStandardEditSelection(IntPtr control, out int start, out int end, out string? selected)
    {
        start = end = 0; selected = null;
        var className = new StringBuilder(128);
        NativeMethods.GetClassName(control, className, className.Capacity);
        if (!className.ToString().Contains("Edit", StringComparison.OrdinalIgnoreCase)) return false;
        NativeMethods.SendMessage(control, NativeMethods.EmGetSel, ref start, ref end);
        if (end <= start) return true;
        var length = NativeMethods.SendMessage(control, NativeMethods.WmGetTextLength, IntPtr.Zero, IntPtr.Zero).ToInt32();
        if (length <= 0 || end > length) return true;
        var buffer = new StringBuilder(length + 1);
        NativeMethods.SendMessage(control, NativeMethods.WmGetText, (IntPtr)buffer.Capacity, buffer);
        selected = buffer.ToString(start, end - start);
        return true;
    }

    private static bool TryAutomationSelection(IntPtr control, out string? selected)
    {
        selected = null;
        try
        {
            var element = AutomationElement.FromHandle(control);
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) return false;
            var ranges = ((TextPattern)pattern).GetSelection();
            if (ranges.Length != 1) return false;
            var value = ranges[0].GetText(-1);
            if (!string.IsNullOrEmpty(value)) selected = value;
            return true;
        }
        catch (ElementNotAvailableException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool SendUnicode(string text)
    {
        var inputs = new List<NativeMethods.Input>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(Key(character, false)); inputs.Add(Key(character, true));
        }
        return NativeMethods.SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<NativeMethods.Input>()) == inputs.Count;
    }

    private static NativeMethods.Input Key(char c, bool up) => new()
    {
        type = NativeMethods.InputKeyboard,
        union = new NativeMethods.InputUnion { keyboard = new NativeMethods.KeyboardInput { scanCode = c, flags = NativeMethods.KeyeventfUnicode | (up ? NativeMethods.KeyeventfKeyup : 0) } }
    };
}
