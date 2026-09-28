namespace ATPrinterAgent;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--service", StringComparison.OrdinalIgnoreCase)))
        {
            ServiceRunner.RunAsync().GetAwaiter().GetResult();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}