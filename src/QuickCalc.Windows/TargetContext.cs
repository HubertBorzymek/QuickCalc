using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace QuickCalc.Windows;

public sealed class TargetContext
{
    private static int _automationProbeInFlight;
    private readonly AutomationElement? _automationTarget;
    private readonly TextPatternRange? _automationRange;

    public TargetContext(
        IntPtr windowHandle,
        IntPtr controlHandle,
        int? selectionStart,
        int? selectionEnd,
        string? selectedText,
        string detectionMethod,
        Rectangle? targetBounds = null)
        : this(windowHandle, controlHandle, selectionStart, selectionEnd, selectedText, detectionMethod,
            null, null, targetBounds)
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
        TextPatternRange? automationRange,
        Rectangle? targetBounds)
    {
        WindowHandle = windowHandle;
        ControlHandle = controlHandle;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
        SelectedText = selectedText;
        DetectionMethod = detectionMethod;
        _automationTarget = automationTarget;
        _automationRange = automationRange;
        TargetBounds = targetBounds;
    }

    public IntPtr WindowHandle { get; }
    public IntPtr ControlHandle { get; }
    public int? SelectionStart { get; }
    public int? SelectionEnd { get; }
    public string? SelectedText { get; }
    public string DetectionMethod { get; }
    public Rectangle? TargetBounds { get; }
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

        var captured = CaptureForHandles(window, control);
        if (captured.HasSelection || captured.DetectionMethod != "brak wiarygodnej detekcji" ||
            !TryReadSelectionByCopy(out var copiedSelection)) return captured;

        return new TargetContext(window, control, null, null, copiedSelection,
            "Ctrl+C (niezależne od UI Automation)", null, null, captured.TargetBounds);
    }

    public static TargetContext CaptureForHandles(IntPtr window, IntPtr control)
    {
        try
        {
            if (TryStandardEditSelection(control, out var start, out var end, out var text))
                return new TargetContext(window, control, start, end, text, "Win32 EM_GETSEL", GetNativeBounds(control));
            var automationStatus = TryAutomationSelectionWithTimeout(control, out text,
                out var automationTarget, out var automationRange, out var automationBounds);
            if (automationStatus == AutomationProbeStatus.Success)
                return new TargetContext(window, control, null, null, text, "UI Automation TextPattern",
                    automationTarget, automationRange, automationBounds ?? GetNativeBounds(control));
            var method = automationStatus == AutomationProbeStatus.Timeout
                ? "limit czasu UI Automation — pominięto analizę"
                : "brak wiarygodnej detekcji";
            return new TargetContext(window, control, null, null, null, method, GetNativeBounds(control));
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
        return new TargetContext(window, control, null, null, null, reason, GetNativeBounds(control));
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

        if (_automationTarget is null)
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
            _automationRange?.Select();
            return true;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or
                                   UnauthorizedAccessException or COMException)
        {
            // UIA is advisory. The editor can invalidate its provider while keeping
            // its native selection, so Ctrl+V still gets a chance to work.
            LastFailureReason = $"UI Automation nie odtworzyło zakresu ({ex.GetType().Name}); użyto zachowanego zaznaczenia.";
            return true;
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
        if (!IsValid)
        {
            LastFailureReason = "Okno docelowe już nie istnieje.";
            return false;
        }
        if (SelectionStart is int start)
        {
            var end = SelectionEnd ?? start;
            NativeMethods.SendMessage(ControlHandle, NativeMethods.EmSetSel, (IntPtr)start, (IntPtr)end);
            NativeMethods.SendMessage(ControlHandle, NativeMethods.EmReplaceSel, (IntPtr)1, value);
            var caret = start + value.Length;
            NativeMethods.SendMessage(ControlHandle, NativeMethods.EmSetSel,
                (IntPtr)(HasSelection ? start : caret), (IntPtr)caret);
            RestoreNativeFocus();
            return true;
        }
        if (!RestoreFocus()) return false;
        try
        {
            using var clipboard = ClipboardSnapshot.Capture();
            SetClipboardText(value);
            if (!SendChord(NativeMethods.VkControl, NativeMethods.VkV))
            {
                LastFailureReason = $"Windows odrzucił skrót Ctrl+V (kod {Marshal.GetLastWin32Error()}).";
                return false;
            }
            WaitForInjectedInput();
            if (HasSelection && !SelectPreviousCharacters(value.Length))
            {
                LastFailureReason = $"Wynik wklejono, ale Windows odrzucił ponowne zaznaczenie (kod {Marshal.GetLastWin32Error()}).";
            }
            WaitForInjectedInput();
            return true;
        }
        catch (ExternalException ex)
        {
            LastFailureReason = $"Schowek jest chwilowo niedostępny: {ex.Message}";
            return false;
        }
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

    private enum AutomationProbeStatus { Success, Unavailable, Timeout }

    private static AutomationProbeStatus TryAutomationSelectionWithTimeout(
        IntPtr control,
        out string? selected,
        out AutomationElement? automationTarget,
        out TextPatternRange? automationRange,
        out Rectangle? automationBounds)
    {
        selected = null; automationTarget = null; automationRange = null; automationBounds = null;
        if (Interlocked.CompareExchange(ref _automationProbeInFlight, 1, 0) != 0)
            return AutomationProbeStatus.Timeout;

        string? workerSelected = null;
        AutomationElement? workerTarget = null;
        TextPatternRange? workerRange = null;
        Rectangle? workerBounds = null;
        var succeeded = false;
        var finished = new ManualResetEventSlim();
        var worker = new Thread(() =>
        {
            try
            {
                succeeded = TryAutomationSelection(control, out workerSelected, out workerTarget, out workerRange);
                if (succeeded) workerBounds = GetAutomationBounds(workerTarget);
            }
            finally { Interlocked.Exchange(ref _automationProbeInFlight, 0); finished.Set(); }
        }) { IsBackground = true, Name = "QuickCalc UI Automation probe" };
        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
        if (!finished.Wait(TimeSpan.FromMilliseconds(45))) return AutomationProbeStatus.Timeout;
        if (!succeeded) return AutomationProbeStatus.Unavailable;
        selected = workerSelected; automationTarget = workerTarget; automationRange = workerRange;
        automationBounds = workerBounds;
        return AutomationProbeStatus.Success;
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

    private static bool TryReadSelectionByCopy(out string selected)
    {
        selected = string.Empty;
        try
        {
            using var clipboard = ClipboardSnapshot.Capture();
            var marker = "QuickCalc/" + Guid.NewGuid().ToString("N");
            SetClipboardText(marker);
            if (!SendChord(NativeMethods.VkControl, NativeMethods.VkC)) return false;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                Application.DoEvents();
                Thread.Sleep(5);
                var value = GetClipboardText();
                if (value == marker) continue;
                if (!string.IsNullOrEmpty(value)) { selected = value; return true; }
                return false;
            }
            return false;
        }
        catch (ExternalException) { return false; }
    }

    private static bool SendChord(ushort modifier, ushort key)
    {
        NativeMethods.Input[] inputs = [VirtualKey(modifier, false), VirtualKey(key, false),
            VirtualKey(key, true), VirtualKey(modifier, true)];
        return NativeMethods.SendInput((uint)inputs.Length, inputs, NativeInputSize) == inputs.Length;
    }

    private static bool SelectPreviousCharacters(int count)
    {
        var inputs = new List<NativeMethods.Input>(count * 2 + 2) { VirtualKey(NativeMethods.VkShift, false) };
        for (var i = 0; i < count; i++)
        {
            inputs.Add(VirtualKey(NativeMethods.VkLeft, false));
            inputs.Add(VirtualKey(NativeMethods.VkLeft, true));
        }
        inputs.Add(VirtualKey(NativeMethods.VkShift, true));
        return NativeMethods.SendInput((uint)inputs.Count, [.. inputs], NativeInputSize) == inputs.Count;
    }

    private static void WaitForInjectedInput()
    {
        // SendInput only queues the keys. Pumping messages matters when the target
        // lives on this UI thread (tests and some embedded editors) and is harmless
        // for external applications.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static Rectangle? GetNativeBounds(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out var rect) ||
            rect.right <= rect.left || rect.bottom <= rect.top) return null;
        return Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
    }

    private static Rectangle? GetAutomationBounds(AutomationElement? element)
    {
        try
        {
            if (element is null) return null;
            var rect = element.Current.BoundingRectangle;
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) return null;
            return Rectangle.FromLTRB((int)Math.Floor(rect.Left), (int)Math.Floor(rect.Top),
                (int)Math.Ceiling(rect.Right), (int)Math.Ceiling(rect.Bottom));
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            return null;
        }
    }

    private static NativeMethods.Input VirtualKey(ushort key, bool up) => new()
    {
        type = NativeMethods.InputKeyboard,
        union = new NativeMethods.InputUnion { keyboard = new NativeMethods.KeyboardInput
            { virtualKey = key, flags = up ? NativeMethods.KeyeventfKeyup : 0 } }
    };

    private static string GetClipboardText() => RetryClipboard(() => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty);
    private static void SetClipboardText(string value) => RetryClipboard(() => { Clipboard.SetText(value); return true; });
    private static T RetryClipboard<T>(Func<T> action)
    {
        ExternalException? last = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try { return action(); }
            catch (ExternalException ex) { last = ex; Thread.Sleep(10); }
        }
        throw last ?? new ExternalException("Schowek jest niedostępny.");
    }

    private sealed class ClipboardSnapshot : IDisposable
    {
        private readonly DataObject? _data;
        private ClipboardSnapshot(DataObject? data) => _data = data;

        public static ClipboardSnapshot Capture()
        {
            var source = RetryClipboard(Clipboard.GetDataObject);
            if (source is null) return new ClipboardSnapshot(null);
            var copy = new DataObject();
            foreach (var format in source.GetFormats(false))
            {
                try
                {
                    var value = source.GetData(format, false);
                    if (value is Stream stream)
                    {
                        var position = stream.CanSeek ? stream.Position : 0;
                        var memory = new MemoryStream(); stream.CopyTo(memory); memory.Position = 0;
                        if (stream.CanSeek) stream.Position = position;
                        value = memory;
                    }
                    else if (value is Image image) value = image.Clone();
                    if (value is not null) copy.SetData(format, false, value);
                }
                catch (Exception ex) when (ex is ExternalException or InvalidOperationException) { }
            }
            return new ClipboardSnapshot(copy);
        }

        public void Dispose()
        {
            try
            {
                if (_data is null) RetryClipboard(() => { Clipboard.Clear(); return true; });
                else RetryClipboard(() => { Clipboard.SetDataObject(_data, true); return true; });
            }
            catch (ExternalException) { }
        }
    }
}
