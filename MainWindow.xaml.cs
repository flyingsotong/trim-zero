using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TrimZero.ViewModels;

namespace TrimZero;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 1;
    private const uint ModControl = 0x0002;
    private const uint ModAlt = 0x0001;
    private const uint VkZ = 0x5A;

    private readonly MainViewModel _vm;
    private readonly bool _openSettings;
    private bool _hotkeyRegistered;

    public MainWindow(bool demo, bool openSettings = false, bool nudge = false)
    {
        InitializeComponent();
        _openSettings = openSettings;
        _vm = new MainViewModel(demo, nudge);
        DataContext = _vm;

        if (_vm.WindowLeft is double left && _vm.WindowTop is double top)
        {
            double vx = SystemParameters.VirtualScreenLeft;
            double vy = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth;
            double vh = SystemParameters.VirtualScreenHeight;
            if (left + 100 > vx && left < vx + vw - 100 && top + 60 > vy && top < vy + vh - 60)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
            }
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        ApplyWindowChrome(hwnd);

        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        _hotkeyRegistered = RegisterHotKey(hwnd, HotkeyId, ModControl | ModAlt, VkZ);
        if (!_hotkeyRegistered)
        {
            // Another app owns the chord. ZERO still works with the mouse.
        }

        _vm.Initialize(hwnd);
        if (_openSettings) _vm.IsSettingsOpen = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            _vm.Zero();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void ApplyWindowChrome(IntPtr hwnd)
    {
        try
        {
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            int round = 2;
            DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
        }
        catch
        {
            // Pre-Windows 11: the custom border still works.
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is System.Windows.Controls.Button) return;
        try { DragMove(); } catch { }
    }

    private void Zero_Click(object sender, RoutedEventArgs e) => _vm.Zero();

    private void Settings_Click(object sender, RoutedEventArgs e) => _vm.IsSettingsOpen = true;

    private void Done_Click(object sender, RoutedEventArgs e) => _vm.IsSettingsOpen = false;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _vm.IsSettingsOpen)
        {
            _vm.IsSettingsOpen = false;
            e.Handled = true;
        }
    }

    private void CycleAxis_Click(object sender, RoutedEventArgs e) => _vm.CycleAxis();

    private void CycleDevice_Click(object sender, RoutedEventArgs e) => _vm.CycleDevice();

    private void SetDown_Click(object sender, RoutedEventArgs e) => _vm.SetTravelDown();

    private void SetUp_Click(object sender, RoutedEventArgs e) => _vm.SetTravelUp();

    private void CycleSpeed_Click(object sender, RoutedEventArgs e) => _vm.CycleSpeed();

    private void Feedback_Click(object sender, MouseButtonEventArgs e) =>
        OpenUrl($"mailto:alan@magentadebrief.com?subject={Uri.EscapeDataString($"Trim Zero feedback ({_vm.VersionText})")}");

    private void Donate_Click(object sender, MouseButtonEventArgs e) =>
        OpenUrl("https://magentadebrief.com/donate/");

    private void Magenta_Click(object sender, MouseButtonEventArgs e) =>
        OpenUrl("https://magentadebrief.com");

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            // A machine with no browser or mail client is not worth interrupting the app for.
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _vm.SaveWindowPosition(Left, Top);
        _vm.Dispose();
        base.OnClosed(e);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);
}
