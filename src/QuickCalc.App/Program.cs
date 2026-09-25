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
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            DiagnosticFileLogger.Write(e.Exception, "Application.ThreadException");
            MessageBox.Show("Wystąpił błąd interfejsu QuickCalc. Szczegóły zapisano w pliku quickcalc-error.log obok programu.",
                "QuickCalc — błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception) DiagnosticFileLogger.Write(exception, "AppDomain.UnhandledException");
        };
        Application.Run(new QuickCalcContext());
    }
}
