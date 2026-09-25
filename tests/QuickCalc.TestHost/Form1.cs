namespace QuickCalc.TestHost;

public sealed class Form1 : Form
{
    public Form1()
    {
        Text = "QuickCalc — aplikacja testowa";
        ClientSize = new Size(720, 500);
        Font = new Font("Segoe UI", 10F);
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 7, AutoScroll = true };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(panel, 0, "Puste pole", "");
        AddRow(panel, 1, "Wartość bez spacji", "25mm");
        AddRow(panel, 2, "Wartość ze spacją", "3 mm");
        AddRow(panel, 3, "Tekst otaczający liczbę", "Position: 123mm");
        AddRow(panel, 4, "Kursor bez zaznaczenia", "Position: 123mm", box => box.SelectionStart = 10);
        AddRow(panel, 5, "Zaznaczone 25mm", "Value: 25mm", box => box.Select(7, 4));
        var multiline = AddRow(panel, 6, "Pole wielowierszowe", "Pierwszy wiersz\r\nTrack width: 100mil\r\nOstatni wiersz");
        multiline.Multiline = true; multiline.Height = 70;
        Controls.Add(panel);
    }

    private static TextBox AddRow(TableLayoutPanel panel, int row, string label, string text, Action<TextBox>? initialize = null)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 9, 3, 3) }, 0, row);
        var box = new TextBox { Text = text, Dock = DockStyle.Top, Margin = new Padding(3, 5, 3, 7) };
        panel.Controls.Add(box, 1, row);
        box.Enter += (_, _) => BeginInvoke(() => initialize?.Invoke(box));
        return box;
    }
}
