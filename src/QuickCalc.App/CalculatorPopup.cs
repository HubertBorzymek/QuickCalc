using QuickCalc.Core;
using QuickCalc.Windows;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QuickCalc.App;

internal enum CalculatorMode { Context, Clipboard }
internal sealed record PopupOperation(CalculatorMode Mode, bool Cancelled, string? Result = null);

internal sealed class CalculatorPopup : Form
{
    private const int CompactWidth = 400;
    private const int CompactHeight = 60;
    private const int CompactErrorHeight = 88;
    private const int ExpandedHeight = 160;
    private const int ExpandedErrorHeight = 187;
    private const int CornerRadius = 14;
    private const int CsDropShadow = 0x00020000;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmRound = 2;

    private static readonly Color Surface = Color.FromArgb(24, 27, 32);
    private static readonly Color SurfaceRaised = Color.FromArgb(42, 47, 55);
    private static readonly Color Border = Color.FromArgb(76, 83, 94);
    private static readonly Color Primary = Color.FromArgb(244, 246, 249);
    private static readonly Color Secondary = Color.FromArgb(173, 181, 193);
    private static readonly Color Muted = Color.FromArgb(119, 129, 143);
    private static readonly Color Error = Color.FromArgb(255, 116, 116);

    private readonly CalculatorMode _mode;
    private readonly TargetContext _target;
    private readonly ExpressionEvaluator _evaluator;
    private readonly ExpressionHistory _history;
    private readonly PillLabel _selection = new();
    private readonly TextBox _expression = new();
    private readonly Label _preview = new();
    private readonly Label _modeDetails = new();
    private readonly Label _resultDetails = new();
    private readonly Label _historyDetails = new();
    private readonly Label _controlsHint = new();
    private readonly Label _error = new();
    private bool _historyNavigation;
    private bool _completionRaised;
    private bool _expanded;

    public event EventHandler<PopupOperation>? OperationFinished;
    internal bool IsExpanded => _expanded;

    public CalculatorPopup(CalculatorMode mode, TargetContext target, ExpressionEvaluator evaluator, ExpressionHistory history)
    {
        _mode = mode;
        _target = target;
        _evaluator = evaluator;
        _history = history;

        Text = "QuickCalc";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Surface;
        ForeColor = Primary;
        Opacity = 0.91;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        _selection.AutoSize = false;
        _selection.TextAlign = ContentAlignment.MiddleCenter;
        _selection.BackColor = SurfaceRaised;
        _selection.ForeColor = Secondary;
        _selection.Font = UiFont(9F, FontStyle.Bold);
        _selection.AutoEllipsis = true;

        _expression.BorderStyle = BorderStyle.None;
        _expression.BackColor = Surface;
        _expression.ForeColor = Primary;
        _expression.Font = UiFont(13F, FontStyle.Bold);
        _expression.TabStop = true;

        _preview.BackColor = Surface;
        _preview.ForeColor = Secondary;
        _preview.Font = UiFont(11F, FontStyle.Regular);
        _preview.TextAlign = ContentAlignment.MiddleLeft;
        _preview.AutoEllipsis = true;
        _preview.Text = "=";

        ConfigureDetailLabel(_modeDetails, Secondary, 9F, FontStyle.Bold);
        ConfigureDetailLabel(_resultDetails, Secondary, 9F, FontStyle.Regular);
        ConfigureDetailLabel(_historyDetails, Muted, 8.5F, FontStyle.Regular);
        ConfigureDetailLabel(_controlsHint, Muted, 8.5F, FontStyle.Regular);
        ConfigureDetailLabel(_error, Error, 8.75F, FontStyle.Regular);

        Controls.AddRange([_selection, _expression, _preview, _modeDetails, _resultDetails,
            _historyDetails, _controlsHint, _error]);

        _expression.TextChanged += (_, _) =>
        {
            if (!_historyNavigation) _history.ResetNavigation();
            _historyNavigation = false;
            RefreshPreview();
        };
        _expression.KeyDown += ExpressionKeyDown;
        Shown += (_, _) =>
        {
            ApplyLayout();
            MoveToPreferredLocation();
            FocusExpression();
        };
        DpiChanged += (_, _) =>
        {
            ApplyLayout();
            MoveToPreferredLocation();
        };
        FormClosing += (_, e) =>
        {
            if (_completionRaised) return;
            e.Cancel = true;
            Finish(new(_mode, true));
        };

        RefreshStaticDetails();
        ApplyLayout();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= CsDropShadow;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            var preference = DwmRound;
            _ = DwmSetWindowAttribute(Handle, DwmWindowCornerPreference, ref preference, sizeof(int));
        }
        UpdateRoundedRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedPath(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), ScalePx(CornerRadius));
        using var pen = new Pen(Border, Math.Max(1, ScalePx(1)));
        e.Graphics.DrawPath(pen, path);
        if (_expanded)
        {
            using var divider = new Pen(Color.FromArgb(49, 55, 64), Math.Max(1, ScalePx(1)));
            e.Graphics.DrawLine(divider, ScalePx(14), ScalePx(59), ClientSize.Width - ScalePx(14), ScalePx(59));
        }
    }

    private void RefreshPreview()
    {
        if (_expression.TextLength == 0)
        {
            _preview.Text = "=";
            _resultDetails.Text = "Wynik pojawi się podczas pisania";
            SetError(null);
            return;
        }

        try
        {
            var result = _evaluator.Evaluate(_expression.Text,
                _mode == CalculatorMode.Context ? _target.SelectedText : null);
            _preview.Text = "= " + result.Text;
            _resultDetails.Text = $"Wynik: {result.Text}  •  {(result.IsRelative ? "relative" : "absolute")}";
            SetError(null);
        }
        catch (CalculationException ex)
        {
            _preview.Text = "=";
            _resultDetails.Text = "Brak poprawnego wyniku";
            SetError(ex.Message);
        }
    }

    private void ExpressionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up && (_expression.TextLength == 0 || _historyNavigation))
        {
            e.SuppressKeyPress = true;
            NavigateHistory(_history.Previous());
        }
        else if (e.KeyCode == Keys.Down && _historyNavigation)
        {
            e.SuppressKeyPress = true;
            NavigateHistory(_history.Next());
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        => HandleCommand(keyData) || base.ProcessCmdKey(ref msg, keyData);

    internal bool HandleCommand(Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Finish(new(_mode, true));
            return true;
        }
        if ((keyData & Keys.KeyCode) == Keys.Enter)
        {
            Submit((keyData & Keys.Shift) == Keys.Shift);
            return true;
        }
        return false;
    }

    internal void ToggleExpanded()
    {
        var selectionStart = _expression.SelectionStart;
        var selectionLength = _expression.SelectionLength;
        _expanded = !_expanded;
        RefreshStaticDetails();
        ApplyLayout();
        MoveToPreferredLocation();
        FocusExpression();
        var safeStart = Math.Min(selectionStart, _expression.TextLength);
        _expression.Select(safeStart, Math.Min(selectionLength, _expression.TextLength - safeStart));
    }

    private void NavigateHistory(string? value)
    {
        if (value is null) return;
        _historyNavigation = true;
        _expression.Text = value;
        _historyNavigation = true;
        _expression.SelectionStart = _expression.TextLength;
    }

    internal void Submit(bool convertToSi)
    {
        try
        {
            var expression = _expression.Text;
            if (convertToSi && string.IsNullOrWhiteSpace(expression))
            {
                if (_mode != CalculatorMode.Context ||
                    !ParsedSelection.TryParse(_target.SelectedText, out var selected) || selected?.Unit is null) return;
                expression = "*1";
            }
            var result = _evaluator.Evaluate(expression,
                _mode == CalculatorMode.Context ? _target.SelectedText : null, convertToSi);
            if (!string.IsNullOrWhiteSpace(_expression.Text)) _history.Add(_expression.Text);
            Finish(new(_mode, false, result.Text));
        }
        catch (CalculationException ex)
        {
            ShowOperationError(ex.Message);
        }
    }

    private void Finish(PopupOperation operation)
    {
        if (_completionRaised) return;
        _completionRaised = true;
        OperationFinished?.Invoke(this, operation);
    }

    public void ShowOperationError(string message)
    {
        _completionRaised = false;
        SetError(message);
        Show();
        Activate();
        FocusExpression();
    }

    public void MoveToPreferredLocation()
    {
        if (_mode == CalculatorMode.Context && _target.TargetBounds is Rectangle targetBounds)
        {
            var targetScreen = Screen.FromRectangle(targetBounds).WorkingArea;
            var looksLikeField = targetBounds.Width < targetScreen.Width * 0.9 &&
                                 targetBounds.Height < targetScreen.Height * 0.6;
            if (looksLikeField)
            {
                var gap = ScalePx(8);
                var y = targetBounds.Bottom + gap;
                if (y + Height > targetScreen.Bottom) y = targetBounds.Top - Height - gap;
                Location = new Point(
                    Math.Clamp(targetBounds.Left, targetScreen.Left, targetScreen.Right - Width),
                    Math.Clamp(y, targetScreen.Top, targetScreen.Bottom - Height));
                return;
            }
        }

        var cursor = Cursor.Position;
        var area = Screen.FromPoint(cursor).WorkingArea;
        var offset = ScalePx(18);
        var x = cursor.X + offset;
        var yNearCursor = cursor.Y + offset;
        if (x + Width > area.Right) x = cursor.X - offset - Width;
        if (yNearCursor + Height > area.Bottom) yNearCursor = cursor.Y - offset - Height;
        Location = new Point(
            Math.Clamp(x, area.Left, area.Right - Width),
            Math.Clamp(yNearCursor, area.Top, area.Bottom - Height));
    }

    private void RefreshStaticDetails()
    {
        var selectedText = _target.HasSelection ? Shorten(_target.SelectedText!, 14) : null;
        _selection.Text = _mode == CalculatorMode.Clipboard ? "CLIP" : selectedText ?? "—";
        _modeDetails.Text = _mode == CalculatorMode.Context
            ? $"CONTEXT  •  zaznaczenie: {(_target.HasSelection ? Shorten(_target.SelectedText!, 28) : "brak")}"
            : "CLIPBOARD  •  wynik zostanie skopiowany";
        _historyDetails.Text = _history.Items.Count == 0
            ? "Historia: brak"
            : "Ostatnie: " + Shorten(_history.Items[^1], 40);
        _controlsHint.Text = "Enter — zastosuj   Shift+Enter — SI   Esc — anuluj   F16 — widok";
    }

    private void ApplyLayout()
    {
        var hasError = !string.IsNullOrEmpty(_error.Text);
        ClientSize = new Size(ScalePx(CompactWidth), ScalePx(_expanded
            ? hasError ? ExpandedErrorHeight : ExpandedHeight
            : hasError ? CompactErrorHeight : CompactHeight));

        var left = ScalePx(12);
        var top = ScalePx(13);
        var rowHeight = ScalePx(32);
        var gap = ScalePx(9);
        var measuredChip = TextRenderer.MeasureText(_selection.Text, _selection.Font).Width + ScalePx(19);
        var chipWidth = Math.Clamp(measuredChip, ScalePx(38), ScalePx(92));
        var resultWidth = ScalePx(105);
        _selection.SetBounds(left, top, chipWidth, rowHeight);
        _preview.SetBounds(ClientSize.Width - left - resultWidth, top, resultWidth, rowHeight);
        var expressionLeft = _selection.Right + gap;
        _expression.SetBounds(expressionLeft, top + ScalePx(5),
            Math.Max(ScalePx(80), _preview.Left - gap - expressionLeft), ScalePx(25));

        _modeDetails.Visible = _expanded;
        _resultDetails.Visible = _expanded;
        _historyDetails.Visible = _expanded;
        _controlsHint.Visible = _expanded;
        if (_expanded)
        {
            var detailLeft = ScalePx(15);
            var detailWidth = ClientSize.Width - ScalePx(30);
            _modeDetails.SetBounds(detailLeft, ScalePx(66), detailWidth, ScalePx(18));
            _resultDetails.SetBounds(detailLeft, ScalePx(87), detailWidth, ScalePx(18));
            _historyDetails.SetBounds(detailLeft, ScalePx(108), detailWidth, ScalePx(17));
            _controlsHint.SetBounds(detailLeft, ScalePx(130), detailWidth, ScalePx(18));
            _error.SetBounds(detailLeft, ScalePx(157), detailWidth, ScalePx(18));
        }
        else
        {
            _error.SetBounds(ScalePx(14), ScalePx(58), ClientSize.Width - ScalePx(28), ScalePx(20));
        }
        _error.Visible = hasError;
        UpdateRoundedRegion();
        Invalidate();
    }

    private void SetError(string? message)
    {
        var changed = !string.Equals(_error.Text, message ?? string.Empty, StringComparison.Ordinal);
        _error.Text = message ?? string.Empty;
        if (changed) ApplyLayout();
    }

    private void FocusExpression()
    {
        Activate();
        _expression.Focus();
    }

    private void UpdateRoundedRegion()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        using var path = CreateRoundedPath(ClientRectangle, ScalePx(CornerRadius));
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    private int ScalePx(int logical) => Math.Max(1, (int)Math.Round(logical * DeviceDpi / 96d));

    private static Font UiFont(float size, FontStyle style)
    {
        try { return new Font("Segoe UI Variable Text", size, style); }
        catch (ArgumentException) { return new Font("Segoe UI", size, style); }
    }

    private static void ConfigureDetailLabel(Label label, Color color, float size, FontStyle style)
    {
        label.BackColor = Surface;
        label.ForeColor = color;
        label.Font = UiFont(size, style);
        label.AutoEllipsis = true;
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string Shorten(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private sealed class PillLabel : Label
    {
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 0 || Height <= 0) return;
            using var path = CreateRoundedPath(ClientRectangle, Height / 2);
            var previous = Region;
            Region = new Region(path);
            previous?.Dispose();
        }
    }
}
