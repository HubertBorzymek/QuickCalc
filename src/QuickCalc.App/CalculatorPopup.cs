using QuickCalc.Core;
using QuickCalc.Windows;

namespace QuickCalc.App;

internal enum CalculatorMode { Context, Clipboard }
internal sealed record PopupOperation(CalculatorMode Mode, bool Cancelled, string? Result = null);

internal sealed class CalculatorPopup : Form
{
    private readonly CalculatorMode _mode;
    private readonly TargetContext _target;
    private readonly ExpressionEvaluator _evaluator;
    private readonly ExpressionHistory _history;
    private readonly TextBox _expression = new();
    private readonly Label _preview = new();
    private readonly Label _error = new();
    private bool _historyNavigation;
    private bool _completionRaised;
    public event EventHandler<PopupOperation>? OperationFinished;

    public CalculatorPopup(CalculatorMode mode, TargetContext target, ExpressionEvaluator evaluator, ExpressionHistory history)
    {
        _mode = mode; _target = target; _evaluator = evaluator; _history = history;
        Text = "QuickCalc"; FormBorderStyle = FormBorderStyle.FixedSingle; ShowInTaskbar = false; TopMost = true;
        MaximizeBox = false; MinimizeBox = false; ClientSize = new Size(390, 145); Font = new Font("Segoe UI", 9F); KeyPreview = true;
        var modeLabel = new Label { Left = 12, Top = 10, Width = 360, Height = 20, Text = mode == CalculatorMode.Context ? "TRYB KONTEKSTOWY" : "TRYB SCHOWKA", ForeColor = Color.DimGray };
        _expression.SetBounds(12, 34, 365, 27); _expression.Font = new Font("Segoe UI", 12F);
        _preview.SetBounds(13, 67, 364, 24); _preview.Font = new Font("Segoe UI Semibold", 11F); _preview.Text = "Wpisz wyrażenie";
        _error.SetBounds(13, 94, 364, 20); _error.ForeColor = Color.Firebrick;
        var selection = target.HasSelection ? $"Zaznaczenie: {Shorten(target.SelectedText!)}" : "Brak wykrytego zaznaczenia";
        var hint = new Label { Left = 13, Top = 119, Width = 364, Height = 18, ForeColor = Color.Gray, Text = $"{selection}  •  Enter: zatwierdź  •  Esc: anuluj" };
        Controls.AddRange([modeLabel, _expression, _preview, _error, hint]);
        _expression.TextChanged += (_, _) => { if (!_historyNavigation) _history.ResetNavigation(); _historyNavigation = false; RefreshPreview(); };
        _expression.KeyDown += ExpressionKeyDown;
        Shown += (_, _) => { MoveToCurrentCursorScreen(); _expression.Focus(); };
        FormClosing += (_, e) =>
        {
            if (_completionRaised) return;
            e.Cancel = true;
            Finish(new(_mode, true));
        };
    }

    private void RefreshPreview()
    {
        if (_expression.TextLength == 0) { _preview.Text = "Wpisz wyrażenie"; _error.Text = ""; return; }
        try { var result = _evaluator.Evaluate(_expression.Text, _mode == CalculatorMode.Context ? _target.SelectedText : null); _preview.Text = "= " + result.Text; _error.Text = ""; }
        catch (CalculationException ex) { _preview.Text = ""; _error.Text = ex.Message; }
    }

    private void ExpressionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up && (_expression.TextLength == 0 || _historyNavigation)) { e.SuppressKeyPress = true; NavigateHistory(_history.Previous()); }
        else if (e.KeyCode == Keys.Down && _historyNavigation) { e.SuppressKeyPress = true; NavigateHistory(_history.Next()); }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Finish(new(_mode, true));
            return true;
        }
        if (keyData == Keys.Enter)
        {
            try
            {
                var result = _evaluator.Evaluate(_expression.Text, _mode == CalculatorMode.Context ? _target.SelectedText : null);
                _history.Add(_expression.Text);
                Finish(new(_mode, false, result.Text));
            }
            catch (CalculationException ex) { ShowOperationError(ex.Message); }
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void NavigateHistory(string? value)
    {
        if (value is null) return;
        _historyNavigation = true; _expression.Text = value; _historyNavigation = true; _expression.SelectionStart = _expression.TextLength;
    }

    private void Finish(PopupOperation operation) { if (_completionRaised) return; _completionRaised = true; OperationFinished?.Invoke(this, operation); }

    public void ShowOperationError(string message) { _completionRaised = false; Show(); Activate(); _expression.Focus(); _error.Text = message; }

    public void MoveToCurrentCursorScreen()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        var point = new Point(Cursor.Position.X + 16, Cursor.Position.Y + 20);
        point.X = Math.Clamp(point.X, area.Left, area.Right - Width); point.Y = Math.Clamp(point.Y, area.Top, area.Bottom - Height); Location = point;
    }
    private static string Shorten(string value) => value.Length <= 24 ? value : value[..21] + "…";
}
