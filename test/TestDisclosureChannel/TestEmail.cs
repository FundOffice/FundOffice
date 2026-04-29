using FMO.Disclosure;
using FMO.Models;
using Microsoft.Win32;

namespace TestDisclosureChannel;

[TestClass]
public sealed class TestEmail
{
    public TestEmail()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@$"Software\Nexus\Debug"))
        {
            if (key != null)
            {
                var workFolder = key.GetValue("WorkingFolder") as string;
                if (!string.IsNullOrWhiteSpace(workFolder))
                {
                    var di = new DirectoryInfo(workFolder);
                    if (di.Exists)
                        Directory.SetCurrentDirectory(di.FullName);
                }
            }
        }
    }

    [TestMethod]
    public void TestSend()
    {
        EmailDisclosureChannel channel = new();
        channel.SendMail(new ManagerDisclosureNotice
        {
            Name = "Test Disclosure",
            File = new SimpleFile
            {
                File = new FileMeta("406eb8fe-2549-4d6b-a235-0cbc8e47fe25", "a.txt", DateTime.Now, "fdslfjdkslfjeri"),
            }
        }, [new Investor { Name = "John Doe", Email = "a@dd.com" },]);






    }
}
