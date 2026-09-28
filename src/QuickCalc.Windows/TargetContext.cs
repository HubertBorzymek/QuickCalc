using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using QuickCalc.Core;
using System.Diagnostics;

namespace QuickCalc.Windows;

public sealed class TargetContext
{
    private static int _automationProbeInFlight;
    private readonly AutomationElement? _automationTarget;
    private readonly TextPatternRange? _automationCaretRange;
    private readonly bool _automationIndicatedSelection;

    public TargetContext(
        IntPtr windowHandle,
        IntPtr controlHandle,
        int? selectionStart,
        int? selectionEnd,
        string? selectedText,
        string detectionMethod,
        Rectangle? targetBounds = null)
        : this(windowHandle, controlHandle, selectionStart, selectionEnd, selectedText, detectionMethod,
            null, null, false, targetBounds)
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
        TextPatternRange? automationCaretRange,
        bool automationIndicatedSelection,
        Rectangle? targetBounds)
    {
        WindowHandle = windowHandle;
        ControlHandle = controlHandle;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
        SelectedText = selectedText;
        DetectionMethod = detectionMethod;
        _automationTarget = automationTarget;
        _automationCaretRange = automationCaretRange;
        _automationIndicatedSelection = automationIndicatedSelection;
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
    public string? LastInsertionDiagnostics { get; private set; }
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
        if (captured.HasSelection) return captured;

        // WM_COPY talks directly to the focused control instead of invoking an
        // application command. It covers native/custom CAD fields without causing
        // Visual Studio's blocking "editor command" dialog when nothing is selected.
        if (TryReadSelectionByWindowMessage(control, out var messageSelection))
            return new TargetContext(window, control, null, null, messageSelection,
                "WM_COPY aktywnej kontrolki", captured._automationTarget, null, true,
                captured.TargetBounds);

        // Never invoke an editor command merely because UIA returned no text.
        // Visual Studio can serialize Ctrl+C behind a busy editor operation and
        // display a long "wait for an editor command" dialog. Use the clipboard
        // probe only when UIA positively reported a non-degenerate range.
        var processName = GetProcessName(window);
        var allowKeyboardCopy = ShouldProbeClipboard(captured.HasSelection, captured._automationIndicatedSelection) ||
                                ShouldUseKeyboardCopyFallback(processName);
        if (!allowKeyboardCopy ||
            !TryReadSelectionByCopy(out var copiedSelection)) return captured;

        return new TargetContext(window, control, null, null, copiedSelection,
            $"Ctrl+C klawiatury (fallback; proces={processName ?? "nieznany"})",
            captured._automationTarget, null, true,
            captured.TargetBounds);
    }

    public static TargetContext CaptureForHandles(IntPtr window, IntPtr control)
    {
        try
        {
            if (TryStandardEditSelection(control, out var start, out var end, out var text))
                return new TargetContext(window, control, start, end, text, "Win32 EM_GETSEL", GetNativeBounds(control));
            var automationStatus = TryAutomationSelectionWithTimeout(control, out text,
                out var automationTarget, out var automationRange, out var automationBounds,
                out var automationIndicatedSelection);
            if (automationStatus == AutomationProbeStatus.Success)
                return new TargetContext(window, control, null, null, text, "UI Automation TextPattern",
                    automationTarget, automationIndicatedSelection ? null : automationRange,
                    automationIndicatedSelection, automationBounds ?? GetNativeBounds(control));
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

    internal static bool ShouldProbeClipboard(bool hasSelection, bool automationIndicatedSelection) =>
        !hasSelection && automationIndicatedSelection;

    internal static bool ShouldUseKeyboardCopyFallback(string? processName) =>
        !string.Equals(processName, "devenv", StringComparison.OrdinalIgnoreCase);

    private static string? GetProcessName(IntPtr window)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(window, out var processId);
            return processId == 0 ? null : Process.GetProcessById((int)processId).ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
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
            // Restore the editable child. A non-degenerate (selected) range is
            // deliberately never re-used because Opera can invalidate it and clear
            // the field. A degenerate range is only a caret position and prevents
            // controls such as Altium fields from selecting all on focus return.
            _automationTarget.SetFocus();
            _automationCaretRange?.Select();
            WaitForInjectedInput(2);
            if (IsTargetFocused(out _)) return true;
            LastFailureReason = BuildFocusFailure("UI Automation nie przywróciło fokusu właściwego pola");
            return false;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or
                                   UnauthorizedAccessException or COMException or ArgumentException)
        {
            // UIA is advisory. The editor can invalidate its provider while keeping
            // its native selection, so Ctrl+V still gets a chance to work.
            if (IsTargetFocused(out _))
            {
                LastFailureReason = $"UI Automation nie odtworzyło zakresu ({ex.GetType().Name}); użyto fokusu natywnego.";
                return true;
            }
            LastFailureReason = BuildFocusFailure($"UI Automation nie odtworzyło pola ({ex.GetType().Name})");
            return false;
        }
    }

    private bool RestoreNativeFocus()
    {
        var targetThread = NativeMethods.GetWindowThreadProcessId(ControlHandle, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();
        var attached = targetThread != currentThread && NativeMethods.AttachThreadInput(currentThread, targetThread, true);
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                NativeMethods.BringWindowToTop(WindowHandle);
                NativeMethods.SetForegroundWindow(WindowHandle);
                NativeMethods.SetActiveWindow(WindowHandle);
                NativeMethods.SetFocus(ControlHandle);
                Application.DoEvents();
                Thread.Sleep(10);
                if (IsTargetForeground() && IsTargetFocused(out _)) return true;
            }
            LastFailureReason = BuildFocusFailure("Windows odmówił przywrócenia fokusu pola docelowego");
            return false;
        }
        finally { if (attached) NativeMethods.AttachThreadInput(currentThread, targetThread, false); }
    }

    public bool InsertOrReplace(string value)
    {
        LastInsertionDiagnostics = null;
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
            LastInsertionDiagnostics = $"EM_REPLACESEL; fokus={FormatHandle(GetTargetFocus())}.";
            return true;
        }
        if (!RestoreFocus()) return false;
        if (!IsTargetFocused(out var focusBeforePaste))
        {
            LastFailureReason = BuildFocusFailure("Nie wysłano Ctrl+V, ponieważ właściwe pole nie odzyskało fokusu");
            return false;
        }
        try
        {
            using var clipboard = ClipboardSnapshot.Capture();
            SetClipboardText(value);
            if (!SendPasteShortcut(out var pasteMethod))
            {
                LastFailureReason = $"Windows odrzucił skrót Ctrl+V (kod {Marshal.GetLastWin32Error()}).";
                return false;
            }
            WaitForInjectedInput();
            if (!IsTargetFocused(out var focusAfterPaste))
            {
                LastInsertionDiagnostics = $"{pasteMethod}; fokus przed={FormatHandle(focusBeforePaste)}, po={FormatHandle(focusAfterPaste)}.";
                LastFailureReason = "Pole docelowe utraciło fokus podczas Ctrl+V. Nie wysłano klawiszy ponownego zaznaczania.";
                return false;
            }
            if (HasSelection && !TrySelectInsertedTextDirectly(value.Length) && !SelectPreviousCharacters(value.Length))
            {
                LastFailureReason = $"Wynik wklejono, ale Windows odrzucił ponowne zaznaczenie (kod {Marshal.GetLastWin32Error()}).";
            }
            WaitForInjectedInput();
            LastInsertionDiagnostics = $"{pasteMethod}; fokus przed={FormatHandle(focusBeforePaste)}, po={FormatHandle(focusAfterPaste)}; " +
                                       $"ponowne zaznaczenie={(HasSelection ? "tak" : "nie")}.";
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
        if (control == IntPtr.Zero) return false;
        var className = new StringBuilder(128);
        NativeMethods.GetClassName(control, className, className.Capacity);
        var classText = className.ToString();
        var knownEditable = classText.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
                            classText.Contains("TextBox", StringComparison.OrdinalIgnoreCase) ||
                            classText.Contains("Scintilla", StringComparison.OrdinalIgnoreCase);
        if (NativeMethods.SendMessageTimeout(control, NativeMethods.EmGetSel, ref start, ref end,
                NativeMethods.SmtoAbortIfHung, 10, out _) == IntPtr.Zero) return false;
        if (!knownEditable && end <= start) return false;
        if (NativeMethods.SendMessageTimeout(control, NativeMethods.WmGetTextLength, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.SmtoAbortIfHung, 10, out var lengthResult) == IntPtr.Zero) return false;
        var length = lengthResult.ToInt32();
        // Some CAD/framework controls implement the Edit message contract despite
        // a custom class name. A non-zero valid range proves that contract without
        // maintaining an application-specific class list.
        if (!knownEditable && (length <= 0 || end > length)) return false;
        if (end <= start) return true;
        if (length <= 0 || end > length) return knownEditable;
        var buffer = new StringBuilder(length + 1);
        if (NativeMethods.SendMessageTimeout(control, NativeMethods.WmGetText, (IntPtr)buffer.Capacity, buffer,
                NativeMethods.SmtoAbortIfHung, 10, out _) == IntPtr.Zero) return knownEditable;
        selected = buffer.ToString(start, end - start);
        return true;
    }

    private enum AutomationProbeStatus { Success, Unavailable, Timeout }

    private static AutomationProbeStatus TryAutomationSelectionWithTimeout(
        IntPtr control,
        out string? selected,
        out AutomationElement? automationTarget,
        out TextPatternRange? automationRange,
        out Rectangle? automationBounds,
        out bool automationIndicatedSelection)
    {
        selected = null; automationTarget = null; automationRange = null; automationBounds = null;
        automationIndicatedSelection = false;
        if (Interlocked.CompareExchange(ref _automationProbeInFlight, 1, 0) != 0)
            return AutomationProbeStatus.Timeout;

        string? workerSelected = null;
        AutomationElement? workerTarget = null;
        TextPatternRange? workerRange = null;
        Rectangle? workerBounds = null;
        var workerIndicatedSelection = false;
        var succeeded = false;
        var finished = new ManualResetEventSlim();
        var worker = new Thread(() =>
        {
            try
            {
                succeeded = TryAutomationSelection(control, out workerSelected, out workerTarget, out workerRange,
                    out workerIndicatedSelection);
                if (succeeded) workerBounds = GetAutomationBounds(workerTarget);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                // A provider can disappear between obtaining the focused element
                // and reading its range. Optional detection must never terminate
                // either QuickCalc or the process hosting an integration test.
                succeeded = false;
            }
            finally { Interlocked.Exchange(ref _automationProbeInFlight, 0); finished.Set(); }
        }) { IsBackground = true, Name = "QuickCalc UI Automation probe" };
        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
        if (!finished.Wait(TimeSpan.FromMilliseconds(45))) return AutomationProbeStatus.Timeout;
        if (!succeeded) return AutomationProbeStatus.Unavailable;
        selected = workerSelected; automationTarget = workerTarget; automationRange = workerRange;
        automationBounds = workerBounds;
        automationIndicatedSelection = workerIndicatedSelection;
        return AutomationProbeStatus.Success;
    }

    private static bool TryAutomationSelection(
        IntPtr control,
        out string? selected,
        out AutomationElement? automationTarget,
        out TextPatternRange? automationRange,
        out bool automationIndicatedSelection)
    {
        selected = null; automationTarget = null; automationRange = null; automationIndicatedSelection = false;
        try
        {
            // Browser/Electron address bars and editors often share a top-level HWND.
            // The globally focused UIA element identifies the actual editable child.
            var focusedElement = AutomationElement.FocusedElement;
            if (!TryGetTextSelectionFromElementOrAncestors(focusedElement, out selected, out automationRange,
                    out automationIndicatedSelection, out var patternElement))
            {
                var handleElement = AutomationElement.FromHandle(control);
                if (!TryGetTextSelectionFromElementOrAncestors(handleElement, out selected, out automationRange,
                        out automationIndicatedSelection, out patternElement)) return false;
            }
            // Refocus the original editable descendant when possible; the range may
            // legitimately be exposed only by one of its document ancestors.
            automationTarget = focusedElement ?? patternElement;
            return true;
        }
        catch (ElementNotAvailableException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (COMException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static bool TryGetTextSelectionFromElementOrAncestors(
        AutomationElement? element,
        out string? selected,
        out TextPatternRange? selectionRange,
        out bool indicatedSelection,
        out AutomationElement? patternElement)
    {
        selected = null; selectionRange = null; indicatedSelection = false; patternElement = null;
        TextPatternRange? caretFallback = null;
        AutomationElement? caretElement = null;
        for (var depth = 0; element is not null && depth < 7; depth++)
        {
            if (TryGetTextSelection(element, out var candidateText, out var candidateRange,
                    out var candidateIndicated))
            {
                if (candidateIndicated || !string.IsNullOrEmpty(candidateText))
                {
                    selected = candidateText;
                    selectionRange = candidateRange;
                    indicatedSelection = candidateIndicated;
                    patternElement = element;
                    return true;
                }
                caretFallback ??= candidateRange;
                caretElement ??= element;
            }
            element = TreeWalker.ControlViewWalker.GetParent(element);
        }
        if (caretFallback is null) return false;
        selectionRange = caretFallback;
        patternElement = caretElement;
        return true;
    }

    private static bool TryGetTextSelection(
        AutomationElement? element,
        out string? selected,
        out TextPatternRange? selectionRange,
        out bool indicatedSelection)
    {
        selected = null; selectionRange = null; indicatedSelection = false;
        if (element is null || !element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) return false;
        var ranges = ((TextPattern)pattern).GetSelection();
        if (ranges.Length != 1) return false;
        selectionRange = ranges[0];
        indicatedSelection = selectionRange.CompareEndpoints(TextPatternRangeEndpoint.Start, selectionRange,
            TextPatternRangeEndpoint.End) != 0;
        var value = selectionRange.GetText(-1);
        if (!string.IsNullOrEmpty(value)) selected = value;
        return true;
    }

    internal static bool TryReadSelectionByCopy(out string selected)
        => TryReadSelectionFromClipboardAction(
            SendCopyShortcut, 6, 5, out selected);

    private static bool SendCopyShortcut()
    {
        try
        {
            System.Windows.Forms.SendKeys.SendWait("^c");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    internal static bool TryReadSelectionByWindowMessage(IntPtr control, out string selected)
        => TryReadSelectionFromClipboardAction(
            () => control != IntPtr.Zero &&
                  NativeMethods.SendMessageTimeout(control, NativeMethods.WmCopy, IntPtr.Zero, IntPtr.Zero,
                      NativeMethods.SmtoAbortIfHung, 10, out _) != IntPtr.Zero,
            3, 3, out selected);

    private static bool TryReadSelectionFromClipboardAction(
        Func<bool> copyAction,
        int attempts,
        int delayMilliseconds,
        out string selected)
    {
        selected = string.Empty;
        try
        {
            using var clipboard = ClipboardSnapshot.Capture();
            var marker = "QuickCalc/" + Guid.NewGuid().ToString("N");
            SetClipboardText(marker);
            if (!copyAction()) return false;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                Application.DoEvents();
                Thread.Sleep(delayMilliseconds);
                var value = GetClipboardText();
                if (value == marker) continue;
                // VS Code copies the entire current line when nothing is selected.
                // Accept only a value the relative-expression parser recognizes,
                // preventing that editor feature from becoming a false selection.
                if (ParsedSelection.TryParse(value, out _)) { selected = value; return true; }
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

    private static bool SendPasteShortcut(out string method)
    {
        try
        {
            System.Windows.Forms.SendKeys.SendWait("^v");
            method = "Ctrl+V przez SendKeys.SendWait";
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            method = "Ctrl+V przez SendInput (fallback po błędzie SendKeys)";
            return SendChord(NativeMethods.VkControl, NativeMethods.VkV);
        }
    }

    private static bool SelectPreviousCharacters(int count)
    {
        if (count <= 0) return true;
        try
        {
            // SendKeys keeps the modifier logically attached to every arrow until
            // the receiving queue has processed it. A raw SendInput batch only
            // guarantees enqueue order; Chromium and some Electron/WinForms queues
            // can otherwise observe Shift-up before handling the arrow messages.
            System.Windows.Forms.SendKeys.SendWait($"+{{LEFT {count}}}");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    private bool TrySelectInsertedTextDirectly(int count)
    {
        if (count <= 0) return true;
        var className = new StringBuilder(128);
        NativeMethods.GetClassName(ControlHandle, className, className.Capacity);
        var classText = className.ToString();
        if (!classText.Contains("Edit", StringComparison.OrdinalIgnoreCase) &&
            !classText.Contains("TextBox", StringComparison.OrdinalIgnoreCase) &&
            !classText.Contains("Scintilla", StringComparison.OrdinalIgnoreCase)) return false;

        var start = 0;
        var end = 0;
        if (NativeMethods.SendMessageTimeout(ControlHandle, NativeMethods.EmGetSel, ref start, ref end,
                NativeMethods.SmtoAbortIfHung, 10, out _) == IntPtr.Zero || end < count) return false;
        NativeMethods.SendMessage(ControlHandle, NativeMethods.EmSetSel, (IntPtr)(end - count), (IntPtr)end);
        return true;
    }

    private static void WaitForInjectedInput(int attempts = 10)
    {
        // SendInput only queues the keys. Pumping messages matters when the target
        // lives on this UI thread (tests and some embedded editors) and is harmless
        // for external applications.
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private bool IsTargetForeground()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == WindowHandle) return true;
        var expectedRoot = NativeMethods.GetAncestor(WindowHandle, NativeMethods.GaRoot);
        var foregroundRoot = NativeMethods.GetAncestor(foreground, NativeMethods.GaRoot);
        return expectedRoot != IntPtr.Zero && expectedRoot == foregroundRoot;
    }

    private bool IsTargetFocused(out IntPtr actualFocus)
    {
        actualFocus = GetTargetFocus();
        return actualFocus == ControlHandle ||
               (actualFocus != IntPtr.Zero && NativeMethods.IsChild(ControlHandle, actualFocus));
    }

    private IntPtr GetTargetFocus()
    {
        var targetThread = NativeMethods.GetWindowThreadProcessId(ControlHandle, out _);
        var info = new NativeMethods.GuiThreadInfo { cbSize = Marshal.SizeOf<NativeMethods.GuiThreadInfo>() };
        return targetThread != 0 && NativeMethods.GetGUIThreadInfo(targetThread, ref info) ? info.hwndFocus : IntPtr.Zero;
    }

    private string BuildFocusFailure(string reason) =>
        $"{reason}; oczekiwano={FormatHandle(ControlHandle)}, fokus={FormatHandle(GetTargetFocus())}, " +
        $"pierwszy plan={FormatHandle(NativeMethods.GetForegroundWindow())}.";

    private static string FormatHandle(IntPtr handle) => $"0x{handle.ToInt64():X}";

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
