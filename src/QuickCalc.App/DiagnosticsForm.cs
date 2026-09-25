namespace QuickCalc.App;

using System.Runtime.InteropServices;

internal sealed class DiagnosticsForm : Form
{
    private readonly Func<string> _reportProvider;
    private readonly Func<HotkeyEditorValues> _settingsProvider;
    private readonly Func<HotkeyEditorValues, string> _applySettings;
    private readonly Action _openCalculator;
    private readonly TextBox _report = new();
    private readonly Label _keyStatus = new();

    public DiagnosticsForm(Func<string> reportProvider, Func<HotkeyEditorValues> settingsProvider,
        Func<HotkeyEditorValues, string> applySettings, Action openCalculator)
    {
        _reportProvider = reportProvider;
        _settingsProvider = settingsProvider;
        _applySettings = applySettings;
        _openCalculator = openCalculator;
        Text = "QuickCalc — diagnostyka";
        ClientSize = new Size(820, 540);
        MinimumSize = new Size(650, 400);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var header = new Label
        {
            Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 10, 0),
            Text = "Naciśnij skonfigurowany skrót. Jeżeli dotrze do QuickCalc, wpis WM_HOTKEY pojawi się w dzienniku."
        };
        _keyStatus.Dock = DockStyle.Top; _keyStatus.Height = 28; _keyStatus.Padding = new Padding(10, 3, 10, 0);
        _keyStatus.Text = "Ostatni klawisz widziany przez to okno: brak";
        _report.Multiline = true; _report.ReadOnly = true; _report.ScrollBars = ScrollBars.Both;
        _report.WordWrap = false; _report.Dock = DockStyle.Fill; _report.Font = new Font("Consolas", 9F);

        var editor = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(8, 2, 8, 2), ColumnCount = 4, RowCount = 3 };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.Controls.Add(new Label { Text = "Tryb", AutoSize = true }, 0, 0);
        editor.Controls.Add(new Label { Text = "Klawisz", AutoSize = true }, 1, 0);
        editor.Controls.Add(new Label { Text = "Modyfikatory", AutoSize = true }, 2, 0);
        var contextKey = new TextBox(); var contextModifiers = new TextBox();
        var clipboardKey = new TextBox(); var clipboardModifiers = new TextBox();
        editor.Controls.Add(new Label { Text = "Kalkulator kontekstowy", AutoSize = true }, 0, 1);
        editor.Controls.Add(contextKey, 1, 1); editor.Controls.Add(contextModifiers, 2, 1);
        editor.Controls.Add(new Label { Text = "Kalkulator schowka", AutoSize = true }, 0, 2);
        editor.Controls.Add(clipboardKey, 1, 2); editor.Controls.Add(clipboardModifiers, 2, 2);
        var apply = new Button { Text = "Zastosuj skróty", AutoSize = true };
        editor.Controls.Add(apply, 3, 1); editor.SetRowSpan(apply, 2);
        var initial = _settingsProvider();
        contextKey.Text = initial.ContextKey; contextModifiers.Text = initial.ContextModifiers;
        clipboardKey.Text = initial.ClipboardKey; clipboardModifiers.Text = initial.ClipboardModifiers;
        contextKey.Width = clipboardKey.Width = 70; contextModifiers.Width = clipboardModifiers.Width = 90;
        contextKey.AccessibleName = "Klawisz kontekstowy"; clipboardKey.AccessibleName = "Klawisz schowka";
        contextModifiers.AccessibleName = "Modyfikatory kontekstowe"; clipboardModifiers.AccessibleName = "Modyfikatory schowka";
        apply.Click += (_, _) =>
        {
            _keyStatus.Text = _applySettings(new(contextKey.Text, contextModifiers.Text, clipboardKey.Text, clipboardModifiers.Text));
            RefreshReport();
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8, 7, 8, 5) };
        var refresh = new Button { Text = "Odśwież", AutoSize = true };
        var calculator = new Button { Text = "Otwórz test kalkulatora", AutoSize = true };
        var copy = new Button { Text = "Kopiuj raport", AutoSize = true };
        refresh.Click += (_, _) => RefreshReport();
        calculator.Click += (_, _) => _openCalculator();
        copy.Click += (_, _) => { try { Clipboard.SetText(_report.Text); } catch (ExternalException ex) { MessageBox.Show(ex.Message, "Błąd schowka"); } };
        buttons.Controls.AddRange([refresh, calculator, copy]);
        Controls.Add(_report); Controls.Add(editor); Controls.Add(_keyStatus); Controls.Add(header); Controls.Add(buttons);
        KeyDown += (_, e) =>
        {
            _keyStatus.Text = $"Ostatni klawisz widziany przez to okno: {e.Modifiers}+{e.KeyCode} (KeyValue={e.KeyValue})";
        };
        Shown += (_, _) => RefreshReport();
    }

    public void RefreshReport()
    {
        if (IsDisposed) return;
        var atEnd = _report.SelectionStart >= Math.Max(0, _report.TextLength - 2);
        _report.Text = _reportProvider();
        if (atEnd) { _report.SelectionStart = _report.TextLength; _report.ScrollToCaret(); }
    }
}
