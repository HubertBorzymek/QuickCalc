using QuickCalc.Core;
using QuickCalc.Windows;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace QuickCalc.App;

internal sealed class QuickCalcContext : ApplicationContext
{
    private readonly GlobalHotkeyWindow _hotkeys = new();
    private readonly ExpressionEvaluator _evaluator = new();
    private readonly ExpressionHistory _history = new();
    private readonly NotifyIcon _tray;
    private CalculatorPopup? _popup;

    public QuickCalcContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Kalkulator schowka", null, (_, _) => Open(CalculatorMode.Clipboard));
        menu.Items.Add("Wyczyść historię", null, (_, _) => _history.Clear());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Zakończ", null, (_, _) => ExitThread());
        _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "QuickCalc — F13: kontekst, Ctrl+F13: schowek", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Open(CalculatorMode.Clipboard);
        try
        {
            var settings = HotkeySettings.Load();
            _hotkeys.Register(1, settings.ContextKey, settings.ContextModifiers);
            _hotkeys.Register(2, settings.ClipboardKey, settings.ClipboardModifiers);
            _hotkeys.Pressed += (_, id) => Open(id == 1 ? CalculatorMode.Context : CalculatorMode.Clipboard);
        }
        catch (Exception ex) { _tray.ShowBalloonTip(8000, "QuickCalc — błąd skrótu", ex.Message, ToolTipIcon.Error); }
    }

    private void Open(CalculatorMode mode)
    {
        if (_popup is { Visible: true }) { _popup.Activate(); return; }
        var target = TargetContext.Capture();
        _popup = new CalculatorPopup(mode, target, _evaluator, _history);
        _popup.OperationFinished += (_, operation) => Complete(target, operation);
        _popup.FormClosed += (_, _) => _popup = null;
        _popup.Show(); _popup.Activate();
    }

    private void Complete(TargetContext target, PopupOperation operation)
    {
        _popup?.Hide(); Application.DoEvents();
        if (operation.Cancelled) { target.RestoreFocus(); _popup?.Close(); return; }
        if (operation.Mode == CalculatorMode.Clipboard)
        {
            try { Clipboard.SetText(operation.Result!); }
            catch (ExternalException) { _popup?.ShowOperationError("Nie można teraz zapisać do schowka."); return; }
            target.RestoreFocus();
        }
        else if (!target.InsertOrReplace(operation.Result!))
        {
            _popup?.ShowOperationError("Nie udało się bezpiecznie przywrócić pola docelowego."); return;
        }
        _popup?.Close();
    }

    protected override void ExitThreadCore()
    {
        _popup?.Close(); _hotkeys.Dispose(); _tray.Visible = false; _tray.Dispose(); base.ExitThreadCore();
    }
}

internal sealed record HotkeySettings(Keys ContextKey, HotkeyModifiers ContextModifiers, Keys ClipboardKey, HotkeyModifiers ClipboardModifiers)
{
    private sealed record JsonSettings(string ContextKey = "F13", string ContextModifiers = "None", string ClipboardKey = "F13", string ClipboardModifiers = "Control");
    public static HotkeySettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "hotkeys.json");
        var dto = File.Exists(path) ? JsonSerializer.Deserialize<JsonSettings>(File.ReadAllText(path)) ?? new() : new();
        return new(ParseKey(dto.ContextKey), ParseModifiers(dto.ContextModifiers), ParseKey(dto.ClipboardKey), ParseModifiers(dto.ClipboardModifiers));
    }
    private static Keys ParseKey(string value) => Enum.TryParse<Keys>(value, true, out var key) ? key : throw new InvalidDataException($"Nieprawidłowy klawisz: {value}");
    private static HotkeyModifiers ParseModifiers(string value)
    {
        var result = HotkeyModifiers.None;
        foreach (var part in value.Split(['+', ',', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            result |= Enum.TryParse<HotkeyModifiers>(part, true, out var modifier) ? modifier : throw new InvalidDataException($"Nieprawidłowy modyfikator: {part}");
        return result;
    }
}
