using System.Threading;
using System.Windows;

namespace TrimZero;

public partial class App : Application
{
    private Mutex? _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instance = new Mutex(initiallyOwned: true, @"Local\TrimZero.SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show("Trim Zero is already running.", "Trim Zero",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        var window = new MainWindow(e.Args.Contains("--demo"), e.Args.Contains("--settings"), e.Args.Contains("--nudge"));
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
