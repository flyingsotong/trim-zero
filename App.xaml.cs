using System.Diagnostics;
using System.Runtime.InteropServices;
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
            FocusExisting();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        bool minimized = e.Args.Contains("--minimized");
        var window = new MainWindow(e.Args.Contains("--demo"), e.Args.Contains("--settings"), e.Args.Contains("--nudge"));
        MainWindow = window;
        window.Show();
        if (minimized) window.WindowState = WindowState.Minimized;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }

    private static void FocusExisting()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            var other = Process.GetProcessesByName(current.ProcessName)
                .FirstOrDefault(p => p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero);
            if (other is not null)
            {
                ShowWindow(other.MainWindowHandle, 9); // SW_RESTORE
                SetForegroundWindow(other.MainWindowHandle);
                return;
            }
        }
        catch
        {
            // Fall through to the message.
        }

        MessageBox.Show("Trim Zero is already running.", "Trim Zero",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
