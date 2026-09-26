using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace QuickCalc.Windows;

public sealed class TargetContext
{
    private readonly AutomationElement? _automationTarget;
    private readonly TextPatternRange? _automationRange;

    public TargetContext(
        IntPtr windowHandle,
        IntPtr controlHandle,
        int? selectionStart,
        int? selectionEnd,
        string? selectedText,
        string detectionMethod)
        : this(windowHandle, controlHandle, selectionStart, selectionEnd, selectedText, detectionMethod, null, null)
    {
    }

    private TargetContext(
        IntPtr windowHandle,
        IntPtr controlHandle,
        int? selectionStart,
        int? selectionEnd,
        string? selectedText,
        string detectionMethod,
        AutomationElement? automationTarget,
        TextPatternRange? automationRange)
    {
        WindowHandle = windowHandle;
        ControlHandle = controlHandle;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
        SelectedText = selectedText;
        DetectionMethod = detectionMethod;
        _automationTarget = automationTarget;
        _automationRange = automationRange;
    }

    public IntPtr WindowHandle { get; }
    public IntPtr ControlHandle { get; }
    public int? SelectionStart { get; }
    public int? SelectionEnd { get; }
    public string? SelectedText { get; }
    public string DetectionMethod { get; }
    public string? LastFailureReason { get; private set; }
    public bool HasSelection => !string.IsNullOrEmpty(SelectedText);
    public static string UiAutomationAssemblyIdentity => typeof(AutomationElement).Assembly.FullName ?? "UIAutomationClient";
    public static int NativeInputSize => Marshal.SizeOf<NativeMethods.Input>();

    public static TargetContext Capture()
    {
        var window = NativeMethods.GetForegroundWindow();
        var thread = NativeMethods.GetWindowThreadProcessId(window, out _);
        var info = new NativeMethods.GuiThreadInfo { cbSize = Marshal.SizeOf<NativeMethods.GuiThreadInfo>() };
        var control = NativeMethods.GetGUIThreadInfo(thread, ref info) && info.hwndFocus != IntPtr.Zero ? info.hwndFocus : window;

        return CaptureForHandles(window, control);
    }

    public static TargetContext CaptureForHandles(IntPtr window, IntPtr control)
    {
        try
        {
            if (TryStandardEditSelection(control, out var start, out var end, out var text))
                return new TargetContext(window, control, start, end, text, "Win32 EM_GETSEL");
            if (TryAutomationSelection(control, out text, out var automationTarget, out var automationRange))
                return new TargetContext(window, control, null, null, text, "UI Automation TextPattern",
                    automationTarget, automationRange);
            return new TargetContext(window, control, null, null, null, "brak wiarygodnej detekcji");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Selection detection is optional. A hostile or broken UI Automation provider
            // must never prevent the calculator itself from opening.
            return new TargetContext(window, control, null, null, null,
                $"tryb bezpieczny po błędzie {ex.GetType().Name}");
        }
    }

    public static TargetContext CaptureBasic(string reason)
    {
        var window = NativeMethods.GetForegroundWindow();
        var thread = NativeMethods.GetWindowThreadProcessId(window, out _);
        var info = new NativeMethods.GuiThreadInfo { cbSize = Marshal.SizeOf<NativeMethods.GuiThreadInfo>() };
        var control = NativeMethods.GetGUIThreadInfo(thread, ref info) && info.hwndFocus != IntPtr.Zero ? info.hwndFocus : window;
        return new TargetContext(window, control, null, null, null, reason);
    }

    public bool IsValid => WindowHandle != IntPtr.Zero && ControlHandle != IntPtr.Zero &&
                           NativeMethods.IsWindow(WindowHandle) && NativeMethods.IsWindow(ControlHandle);

    public bool RestoreFocus()
    {
        LastFailureReason = null;
        if (!IsValid || !NativeMethods.IsWindowEnabled(WindowHandle))
        {
            LastFailureReason = "Okno docelowe już nie istnieje lub jest wyłączone.";
            return false;
        }

        if (!RestoreNativeFocus()) return false;

        if (_automationTarget is null || _automationRange is null)
        {
            if (SelectionStart is int start && SelectionEnd is int end)
                NativeMethods.SendMessage(ControlHandle, NativeMethods.EmSetSel, (IntPtr)start, (IntPtr)end);
            return true;
        }

        try
        {
            // Browser and Electron editors normally expose only their top-level HWND.
            // Restore the real editable child and the exact selection/caret through UIA.
            _automationTarget.SetFocus();
            _automationRange.Select();

            if (!_automationTarget.TryGetCurrentPattern(TextPattern.Pattern, out var pattern))
            {
                LastFailureReason = "Pole docelowe utraciło obsługę UI Automation TextPattern.";
                return false;
            }

            var current = ((TextPattern)pattern).GetSelection();
            if (current.Length != 1 || current[0].GetText(-1) != (SelectedText ?? string.Empty))
            {
                LastFailureReason = "UI Automation nie potwierdziło odtworzenia pierwotnego zaznaczenia lub kursora.";
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or
                                   UnauthorizedAccessException or COMException)
        {
            LastFailureReason = $"Nie udało się odtworzyć zakresu UI Automation: {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private bool RestoreNativeFocus()
    {
        var targetThread = NativeMethods.GetWindowThreadProcessId(WindowHandle, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();
        var attached = targetThread != currentThread && NativeMethods.AttachThreadInput(currentThread, targetThread, true);
        try
        {
            NativeMethods.BringWindowToTop(WindowHandle);
            NativeMethods.SetForegroundWindow(WindowHandle);
            NativeMethods.SetActiveWindow(WindowHandle);
            NativeMethods.SetFocus(ControlHandle);
            if (NativeMethods.GetForegroundWindow() == WindowHandle) return true;
            LastFailureReason = "Windows odmówił przywrócenia okna docelowego na pierwszy plan.";
            return false;
        }
        finally { if (attached) NativeMethods.AttachThreadInput(currentThread, targetThread, false); }
    }

    public bool InsertOrReplace(string value)
    {
        if (!IsValid) return false;
        if (SelectionStart.HasValue)
        {
            NativeMethods.SendMessage(ControlHandle, NativeMethods.EmSetSel,
                (IntPtr)SelectionStart.Value, (IntPtr)(SelectionEnd ?? SelectionStart.Value));
            NativeMethods.SendMessage(ControlHandle, NativeMethods.EmReplaceSel, (IntPtr)1, value);
            RestoreFocus();
            return true;
        }
        if (!RestoreFocus()) return false;
        if (SendUnicode(value)) return true;
        LastFailureReason = $"SendInput nie wstawił wszystkich znaków (kod Win32: {Marshal.GetLastWin32Error()}).";
        return false;
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

    private static bool TryAutomationSelection(
        IntPtr control,
        out string? selected,
        out AutomationElement? automationTarget,
        out TextPatternRange? automationRange)
    {
        selected = null; automationTarget = null; automationRange = null;
        try
        {
            // Browser/Electron address bars and editors often share a top-level HWND.
            // The globally focused UIA element identifies the actual editable child.
            var element = AutomationElement.FocusedElement;
            if (!TryGetTextSelection(element, out selected, out automationRange))
            {
                element = AutomationElement.FromHandle(control);
                if (!TryGetTextSelection(element, out selected, out automationRange)) return false;
            }
            automationTarget = element;
            return true;
        }
        catch (ElementNotAvailableException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (COMException) { return false; }
    }

    private static bool TryGetTextSelection(
        AutomationElement? element,
        out string? selected,
        out TextPatternRange? selectionRange)
    {
        selected = null; selectionRange = null;
        if (element is null || !element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) return false;
        var ranges = ((TextPattern)pattern).GetSelection();
        if (ranges.Length != 1) return false;
        selectionRange = ranges[0];
        var value = selectionRange.GetText(-1);
        if (!string.IsNullOrEmpty(value)) selected = value;
        return true;
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
