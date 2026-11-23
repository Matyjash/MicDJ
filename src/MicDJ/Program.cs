namespace MicDJ;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(true, @"Local\MicDJ", out var first);
        if (!first)
        {
            MessageBox.Show("MicDJ is already running.", "MicDJ");
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
