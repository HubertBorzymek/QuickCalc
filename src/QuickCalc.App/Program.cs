namespace QuickCalc.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "QuickCalc.Singleton.2F858A03", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("QuickCalc jest już uruchomiony.", "QuickCalc", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new QuickCalcContext());
    }
}
