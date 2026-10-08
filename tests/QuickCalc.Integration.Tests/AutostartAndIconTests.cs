using QuickCalc.App;

namespace QuickCalc.Integration.Tests;

[TestClass]
public sealed class AutostartAndIconTests
{
    [STATestMethod]
    public void ShortcutIsCreatedForCurrentExecutableAndCanBeRead()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QuickCalc-lnk-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var link = Path.Combine(directory, "QuickCalc.lnk");
            Assert.IsNull(AutostartShortcut.ReadTarget(link));

            AutostartShortcut.Enable(link);

            Assert.IsTrue(File.Exists(link));
            Assert.AreEqual(Path.GetFullPath(Environment.ProcessPath!),
                Path.GetFullPath(AutostartShortcut.ReadTarget(link)!), ignoreCase: true);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void EmbeddedIconProvidesSharpTraySize()
    {
        using var small = AppIcon.Load(new Size(16, 16));
        using var large = AppIcon.Load(new Size(32, 32));
        Assert.AreEqual(16, small.Width);
        Assert.AreEqual(32, large.Width);
    }
}
