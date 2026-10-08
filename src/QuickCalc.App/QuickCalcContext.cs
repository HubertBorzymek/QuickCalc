using QuickCalc.Core;
using QuickCalc.Windows;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;

namespace QuickCalc.App;

internal sealed class QuickCalcContext : ApplicationContext
{
    private readonly GlobalHotkeyWindow _hotkeys = new();
    private readonly ExpressionEvaluator _evaluator = new();
    private readonly ExpressionHistory _history = new();
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _closeOnFocusLossItem;
    private readonly System.Windows.Forms.Timer _popupGuard = new() { Interval = 750 };
    private readonly List<string> _diagnosticLog = [];
    private readonly Dictionary<int, string> _hotkeyStatus = [];
    private CalculatorPopup? _popup;
    private DiagnosticsForm? _diagnostics;
    private HotkeySettings? _settings;

    public QuickCalcContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Kalkulator kontekstowy", null, (_, _) => Open(CalculatorMode.Context));
        menu.Items.Add("Kalkulator schowka", null, (_, _) => Open(CalculatorMode.Clipboard));
        menu.Items.Add("Diagnostyka…", null, (_, _) => ShowDiagnostics());
        _closeOnFocusLossItem = new ToolStripMenuItem("Zamykaj popup po utracie fokusu") { CheckOnClick = true };
        _closeOnFocusLossItem.CheckedChanged += (_, _) => SetCloseOnFocusLoss(_closeOnFocusLossItem.Checked);
        menu.Items.Add(_closeOnFocusLossItem);
        menu.Items.Add("Resetuj popupy", null, (_, _) => ResetPopups());
        menu.Items.Add("Wyczyść historię", null, (_, _) => _history.Clear());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Zakończ", null, (_, _) => ExitThread());
        _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "QuickCalc — F16: kontekst, Ctrl+F16: schowek", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Open(CalculatorMode.Clipboard);
        _popupGuard.Tick += (_, _) =>
        {
            EnsureSinglePopup();
            _popup?.CloseIfInactive(CalculatorPopup.GetForegroundWindow(), TimeSpan.FromSeconds(1.5));
        };
        _popupGuard.Start();
        _hotkeys.Pressed += (_, id) =>
        {
            try
            {
                AddDiagnostic($"Odebrano WM_HOTKEY, identyfikator {id}.");
                EnsureSinglePopup();
                if (id == 1 && _popup is { Visible: true })
                {
                    _popup.ToggleExpanded();
                    AddDiagnostic($"Przełączono popup na widok {(_popup.IsExpanded ? "Expanded" : "Compact")}.");
                }
                else if (id == 1) Open(CalculatorMode.Context);
                else if (id == 2) Open(CalculatorMode.Clipboard);
                else AddDiagnostic($"Nieznany identyfikator skrótu: {id}.");
            }
            catch (Exception ex)
            {
                AddDiagnostic("Nieobsłużony błąd reakcji na skrót: " + ex);
                DiagnosticFileLogger.Write(ex, "Obsługa WM_HOTKEY");
                _tray.ShowBalloonTip(10000, "QuickCalc — błąd otwierania", "Szczegóły są dostępne w oknie Diagnostyka.", ToolTipIcon.Error);
            }
        };
        try
        {
            _settings = HotkeySettings.Load();
            _closeOnFocusLossItem.Checked = _settings.ClosePopupOnFocusLoss;
            AddDiagnostic($"Wczytano konfigurację z: {_settings.SourcePath}");
            RegisterShortcut(1, "kontekstowy", _settings.ContextKey, _settings.ContextModifiers);
            RegisterShortcut(2, "schowka", _settings.ClipboardKey, _settings.ClipboardModifiers);
        }
        catch (Exception ex)
        {
            _hotkeyStatus[1] = _hotkeyStatus[2] = "BŁĄD KONFIGURACJI: " + ex.Message;
            AddDiagnostic("Błąd konfiguracji: " + ex);
            _tray.ShowBalloonTip(8000, "QuickCalc — błąd konfiguracji", ex.Message, ToolTipIcon.Error);
        }
    }

    private void RegisterShortcut(int id, string name, Keys key, HotkeyModifiers modifiers)
    {
        var shortcut = GlobalHotkeyWindow.Format(modifiers, key);
        try
        {
            _hotkeys.Register(id, key, modifiers);
            _hotkeyStatus[id] = $"OK — {shortcut}";
            AddDiagnostic($"Skrót {name} zarejestrowany: {shortcut}.");
        }
        catch (Exception ex)
        {
            _hotkeyStatus[id] = $"BŁĄD — {shortcut}: {ex.Message}";
            AddDiagnostic($"Błąd skrótu {name} ({shortcut}): {ex}");
            _tray.ShowBalloonTip(8000, $"QuickCalc — błąd skrótu {name}", ex.Message, ToolTipIcon.Error);
        }
    }

    private void Open(CalculatorMode mode)
    {
        var openingTimer = Stopwatch.StartNew();
        AddDiagnostic($"Rozpoczynam otwieranie trybu {mode}.");
        EnsureSinglePopup();
        if (_popup is { IsDisposed: false, Visible: true })
        {
            AddDiagnostic("Popup już istnieje — przenoszę go na bieżący ekran i aktywuję.");
            _popup.MoveToPreferredLocation(); _popup.BringToFront(); _popup.Activate(); return;
        }
        if (_popup is { IsDisposed: false })
        {
            AddDiagnostic("Usuwam ukryty popup przed otwarciem nowego.");
            ClosePopup();
        }

        TargetContext target;
        try
        {
            target = mode == CalculatorMode.Clipboard
                ? TargetContext.CaptureBasic("tryb schowka — bez analizy zaznaczenia")
                : TargetContext.Capture();
        }
        catch (Exception ex)
        {
            AddDiagnostic("Pobranie kontekstu nie powiodło się; używam trybu bezpiecznego: " + ex);
            DiagnosticFileLogger.Write(ex, "TargetContext.Capture");
            try { target = TargetContext.CaptureBasic($"tryb bezpieczny po błędzie {ex.GetType().Name}"); }
            catch (Exception fallbackException)
            {
                AddDiagnostic("Nie udało się nawet pobrać uchwytu okna; popup zostanie pokazany bez celu: " + fallbackException);
                DiagnosticFileLogger.Write(fallbackException, "TargetContext.CaptureBasic");
                target = new TargetContext(IntPtr.Zero, IntPtr.Zero, null, null, null, "tryb awaryjny bez celu");
            }
        }
        AddDiagnostic($"Otwarcie trybu {mode}; cel=0x{target.WindowHandle.ToInt64():X}, kontrolka=0x{target.ControlHandle.ToInt64():X}, metoda={target.DetectionMethod}, zaznaczenie={target.HasSelection}, poprawny={target.IsValid}, przechwycenie={openingTimer.ElapsedMilliseconds} ms.");
        try
        {
            var popup = new CalculatorPopup(mode, target, _evaluator, _history);
            _popup = popup;
            popup.CloseOnFocusLoss = _settings?.ClosePopupOnFocusLoss ?? false;
            popup.OperationFinished += (_, operation) => Complete(popup, target, operation);
            popup.FormClosed += (_, _) =>
            {
                if (ReferenceEquals(_popup, popup)) _popup = null;
            };
            popup.Show(); popup.MoveToPreferredLocation(); popup.BringToFront(); popup.Activate();
            EnsureSinglePopup();
            AddDiagnostic($"Popup pokazany po {openingTimer.ElapsedMilliseconds} ms: uchwyt=0x{_popup.Handle.ToInt64():X}, Visible={_popup.Visible}, Bounds={_popup.Bounds}.");
        }
        catch (Exception ex)
        {
            AddDiagnostic("Nie udało się utworzyć lub pokazać popupu: " + ex);
            DiagnosticFileLogger.Write(ex, "CalculatorPopup.Show");
            _popup?.Dispose(); _popup = null;
            throw;
        }
    }

    private void Complete(CalculatorPopup source, TargetContext target, PopupOperation operation)
    {
        source.Hide(); Application.DoEvents();
        if (operation.Cancelled)
        {
            if (operation.RestoreFocus) target.RestoreFocus();
            ClosePopup(source);
            return;
        }
        if (operation.Mode == CalculatorMode.Clipboard)
        {
            try { Clipboard.SetText(operation.Result!); }
            catch (ExternalException ex) { AddDiagnostic("Błąd schowka: " + ex); source.ShowOperationError("Nie można teraz zapisać do schowka."); return; }
            target.RestoreFocus();
            AddDiagnostic("Wynik zapisano w schowku, przywrócono fokus.");
        }
        else if (!target.InsertOrReplace(operation.Result!))
        {
            AddDiagnostic("Odmowa wstawienia: " + (target.LastFailureReason ??
                "nie udało się bezpiecznie przywrócić celu lub zakresu zaznaczenia.") +
                (target.LastInsertionDiagnostics is null ? "" : " Szczegóły: " + target.LastInsertionDiagnostics));
            source.ShowOperationError("Nie udało się bezpiecznie przywrócić pola docelowego."); return;
        }
        else AddDiagnostic(target.LastFailureReason is null
            ? "Wynik wstawiono do kontrolki docelowej." +
              (target.LastInsertionDiagnostics is null ? "" : " Szczegóły: " + target.LastInsertionDiagnostics)
            : "Wynik wstawiono; ostrzeżenie: " + target.LastFailureReason +
              (target.LastInsertionDiagnostics is null ? "" : " Szczegóły: " + target.LastInsertionDiagnostics));
        ClosePopup(source);
    }

    private void ClosePopup(CalculatorPopup? requested = null)
    {
        var popup = requested ?? _popup;
        if (popup is null) return;
        popup.CloseImmediately();
        if (ReferenceEquals(_popup, popup)) _popup = null;
    }

    private void EnsureSinglePopup()
    {
        var previous = _popup;
        _popup = CalculatorPopup.KeepSingleOpen(_popup);
        if (previous is not null && !ReferenceEquals(previous, _popup))
            AddDiagnostic("Wykryto i usunięto nadmiarowy albo nieaktualny popup.");
    }

    private void ResetPopups()
    {
        var count = CalculatorPopup.CloseAllOpenPopups();
        _popup = null;
        AddDiagnostic($"Reset popupów: zamknięto {count} okien.");
    }

    private void ShowDiagnostics()
    {
        if (_diagnostics is { IsDisposed: false }) { _diagnostics.Show(); _diagnostics.Activate(); _diagnostics.RefreshReport(); return; }
        _diagnostics = new DiagnosticsForm(BuildDiagnosticReport, GetHotkeyEditorValues, ApplyHotkeys, () => Open(CalculatorMode.Clipboard));
        _diagnostics.FormClosed += (_, _) => _diagnostics = null;
        _diagnostics.Show(); _diagnostics.Activate();
        AddDiagnostic("Otwarto okno diagnostyczne.");
    }

    private void SetCloseOnFocusLoss(bool enabled)
    {
        if (_settings is null) return;
        if (_settings.ClosePopupOnFocusLoss == enabled)
        {
            if (_popup is not null) _popup.CloseOnFocusLoss = enabled;
            return;
        }
        _settings = _settings with { ClosePopupOnFocusLoss = enabled };
        _settings.Save();
        if (_popup is not null) _popup.CloseOnFocusLoss = enabled;
        AddDiagnostic($"Zamykanie popupu po utracie fokusu: {(enabled ? "włączone" : "wyłączone")}.");
    }

    private string BuildDiagnosticReport()
    {
        var process = Process.GetCurrentProcess();
        var config = _settings is null ? "nie wczytano" :
            $"kontekst={GlobalHotkeyWindow.Format(_settings.ContextModifiers, _settings.ContextKey)}, schowek={GlobalHotkeyWindow.Format(_settings.ClipboardModifiers, _settings.ClipboardKey)}";
        return $"QuickCalc — diagnostyka\r\n" +
               $"Czas lokalny: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
               $"Proces: PID {Environment.ProcessId}, sesja Windows {process.SessionId}, 64-bit={Environment.Is64BitProcess}\r\n" +
               $"System: {Environment.OSVersion}\r\n" +
               $"Plik EXE: {Environment.ProcessPath}\r\n" +
               $"Konfiguracja: {config}\r\n" +
               $"Skrót kontekstowy: {_hotkeyStatus.GetValueOrDefault(1, "BRAK DANYCH")}\r\n" +
               $"Skrót schowka: {_hotkeyStatus.GetValueOrDefault(2, "BRAK DANYCH")}\r\n\r\n" +
               "Dziennik (bez treści zaznaczenia i schowka):\r\n" + string.Join("\r\n", _diagnosticLog);
    }

    private HotkeyEditorValues GetHotkeyEditorValues() => _settings is null
        ? new("F16", "None", "F16", "Control")
        : new(_settings.ContextKey.ToString(), HotkeySettings.FormatModifiers(_settings.ContextModifiers),
            _settings.ClipboardKey.ToString(), HotkeySettings.FormatModifiers(_settings.ClipboardModifiers));

    private string ApplyHotkeys(HotkeyEditorValues values)
    {
        HotkeySettings requested;
        try { requested = HotkeySettings.FromValues(values,
            _settings?.SourcePath ?? Path.Combine(AppContext.BaseDirectory, "hotkeys.json"),
            _settings?.ClosePopupOnFocusLoss ?? false); }
        catch (Exception ex) { AddDiagnostic("Odrzucono konfigurację skrótów: " + ex.Message); return "Błąd: " + ex.Message; }

        var previous = _settings;
        _hotkeys.Unregister(1); _hotkeys.Unregister(2);
        try
        {
            RegisterShortcutOrThrow(1, "kontekstowy", requested.ContextKey, requested.ContextModifiers);
            RegisterShortcutOrThrow(2, "schowka", requested.ClipboardKey, requested.ClipboardModifiers);
            requested.Save();
            _settings = requested;
            _tray.Text = $"QuickCalc — {GlobalHotkeyWindow.Format(requested.ContextModifiers, requested.ContextKey)}: kontekst, {GlobalHotkeyWindow.Format(requested.ClipboardModifiers, requested.ClipboardKey)}: schowek";
            AddDiagnostic("Zastosowano i zapisano nową konfigurację skrótów.");
            return "Skróty zastosowano i zapisano.";
        }
        catch (Exception ex)
        {
            _hotkeys.Unregister(1); _hotkeys.Unregister(2);
            _hotkeyStatus[1] = _hotkeyStatus[2] = "przywracanie poprzedniej konfiguracji";
            if (previous is not null)
            {
                RegisterShortcut(1, "kontekstowy (przywrócony)", previous.ContextKey, previous.ContextModifiers);
                RegisterShortcut(2, "schowka (przywrócony)", previous.ClipboardKey, previous.ClipboardModifiers);
            }
            AddDiagnostic("Nie zastosowano nowych skrótów: " + ex);
            return "Nie zastosowano zmian: " + ex.Message;
        }
    }

    private void RegisterShortcutOrThrow(int id, string name, Keys key, HotkeyModifiers modifiers)
    {
        var shortcut = GlobalHotkeyWindow.Format(modifiers, key);
        _hotkeys.Register(id, key, modifiers);
        _hotkeyStatus[id] = $"OK — {shortcut}";
        AddDiagnostic($"Skrót {name} zarejestrowany: {shortcut}.");
    }

    private void AddDiagnostic(string message)
    {
        _diagnosticLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        if (_diagnosticLog.Count > 300) _diagnosticLog.RemoveAt(0);
        _diagnostics?.RefreshReport();
    }

    protected override void ExitThreadCore()
    {
        _popupGuard.Stop(); _popupGuard.Dispose(); ResetPopups(); _diagnostics?.Close(); _hotkeys.Dispose();
        _tray.Visible = false; _tray.Dispose(); base.ExitThreadCore();
    }
}

internal sealed record HotkeySettings(Keys ContextKey, HotkeyModifiers ContextModifiers, Keys ClipboardKey,
    HotkeyModifiers ClipboardModifiers, string SourcePath, bool ClosePopupOnFocusLoss = false)
{
    private sealed record JsonSettings(string ContextKey = "F16", string ContextModifiers = "None",
        string ClipboardKey = "F16", string ClipboardModifiers = "Control", bool ClosePopupOnFocusLoss = false);
    public static HotkeySettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "hotkeys.json");
        var dto = File.Exists(path) ? JsonSerializer.Deserialize<JsonSettings>(File.ReadAllText(path)) ?? new() : new();
        return new(ParseKey(dto.ContextKey), ParseModifiers(dto.ContextModifiers), ParseKey(dto.ClipboardKey),
            ParseModifiers(dto.ClipboardModifiers), path, dto.ClosePopupOnFocusLoss);
    }
    public static HotkeySettings FromValues(HotkeyEditorValues values, string path, bool closePopupOnFocusLoss = false) => new(
        ParseKey(values.ContextKey), ParseModifiers(values.ContextModifiers),
        ParseKey(values.ClipboardKey), ParseModifiers(values.ClipboardModifiers), path, closePopupOnFocusLoss);

    public void Save()
    {
        var json = JsonSerializer.Serialize(new JsonSettings(ContextKey.ToString(), FormatModifiers(ContextModifiers),
            ClipboardKey.ToString(), FormatModifiers(ClipboardModifiers), ClosePopupOnFocusLoss),
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SourcePath, json + Environment.NewLine);
    }

    public static string FormatModifiers(HotkeyModifiers value)
    {
        value &= ~HotkeyModifiers.NoRepeat;
        return value == HotkeyModifiers.None ? "None" : value.ToString().Replace(", ", "+");
    }

    private static Keys ParseKey(string value) => Enum.TryParse<Keys>(value.Trim(), true, out var key) && key != Keys.None
        ? key : throw new InvalidDataException($"Nieprawidłowy klawisz: {value}");
    private static HotkeyModifiers ParseModifiers(string value)
    {
        var result = HotkeyModifiers.None;
        foreach (var part in value.Split(['+', ',', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            result |= Enum.TryParse<HotkeyModifiers>(part, true, out var modifier) ? modifier : throw new InvalidDataException($"Nieprawidłowy modyfikator: {part}");
        return result;
    }
}

internal sealed record HotkeyEditorValues(string ContextKey, string ContextModifiers, string ClipboardKey, string ClipboardModifiers);
