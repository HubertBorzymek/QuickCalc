namespace QuickCalc.App;

using QuickCalc.Core;
using QuickCalc.Windows;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                _ = TargetContext.UiAutomationAssemblyIdentity;
                if (new ExpressionEvaluator().Evaluate("+3", "23").Text != "26") return 3;
                return 0;
            }
            catch (Exception ex)
            {
                DiagnosticFileLogger.Write(ex, "Published self-test");
                return 2;
            }
        }
        using var mutex = new Mutex(true, "QuickCalc.Singleton.2F858A03", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("QuickCalc jest już uruchomiony.", "QuickCalc", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
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
        return 0;
    }

}
