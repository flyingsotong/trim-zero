using System.Runtime.InteropServices;
using Vortice.DirectInput;

namespace TrimZero.Services;

/// <summary>
/// Reads one axis of one DirectInput device on a background thread. 250 Hz is
/// enough to catch a hand-driven wheel without putting anything on the UI thread.
/// </summary>
public sealed class WheelService : IDisposable
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint period);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint period);

    private readonly object _gate = new();
    private IDirectInput8? _di;
    private IDirectInputDevice8? _device;
    private Thread? _thread;
    private volatile bool _running;
    private IntPtr _hwnd;
    private string _preferredProduct = "Meza Trim";
    private string _axisName = "X Axis";
    private readonly List<string> _axes = new();

    public event Action<int>? Sample;
    public event Action? Changed;

    public string DeviceName { get; private set; } = "No wheel";
    public string AxisName => _axisName;
    public bool IsConnected { get { lock (_gate) return _device is not null; } }
    public IReadOnlyList<string> AxisNames { get { lock (_gate) return _axes.ToArray(); } }

    public void Start(IntPtr hwnd, string product, string axisName)
    {
        _hwnd = hwnd;
        _preferredProduct = string.IsNullOrWhiteSpace(product) ? "Meza Trim" : product;
        _axisName = string.IsNullOrWhiteSpace(axisName) ? "X Axis" : axisName;
        _running = true;
        TimeBeginPeriod(1);
        _thread = new Thread(PollLoop) { IsBackground = true, Name = "TrimZero.Wheel" };
        _thread.Start();
    }

    public void CycleAxis()
    {
        lock (_gate)
        {
            if (_axes.Count < 2) return;
            int i = _axes.FindIndex(a => string.Equals(a, _axisName, StringComparison.OrdinalIgnoreCase));
            _axisName = _axes[(i + 1 + _axes.Count) % _axes.Count];
        }
        Changed?.Invoke();
    }

    private void PollLoop()
    {
        while (_running)
        {
            if (_device is null)
            {
                if (!TryConnect())
                {
                    Thread.Sleep(1500);
                    continue;
                }
            }

            try
            {
                _device!.Poll();
                var state = _device.GetCurrentJoystickState();
                Sample?.Invoke(ReadAxis(state, _axisName));
                Thread.Sleep(4);
            }
            catch
            {
                // Unplugged or lost acquisition. Drop it and reconnect.
                DropDevice();
                Thread.Sleep(800);
            }
        }
    }

    private bool TryConnect()
    {
        try
        {
            var di = DInput.DirectInput8Create();
            var devices = di.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly)
                .OrderByDescending(d => Matches(d))
                .ToList();
            if (devices.Count == 0)
            {
                di.Dispose();
                SetDevice("No wheel");
                return false;
            }

            var chosen = devices.FirstOrDefault(Matches);
            if (chosen is null)
            {
                di.Dispose();
                SetDevice("No wheel");
                return false;
            }
            var device = di.CreateDevice(chosen.InstanceGuid);
            device.SetDataFormat<RawJoystickState>();
            device.SetCooperativeLevel(_hwnd, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
            device.Acquire();

            var axes = device.GetObjects(DeviceObjectTypeFlags.Axis)
                .Select(o => o.Name?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .ToList();

            lock (_gate)
            {
                _di = di;
                _device = device;
                _axes.Clear();
                _axes.AddRange(axes);
                if (axes.Count > 0 && !axes.Any(a => string.Equals(a, _axisName, StringComparison.OrdinalIgnoreCase)))
                    _axisName = axes[0];
            }

            SetDevice(chosen.InstanceName?.Trim() ?? chosen.ProductName?.Trim() ?? "Wheel");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool Matches(DeviceInstance d)
    {
        var name = (d.InstanceName ?? "") + " " + (d.ProductName ?? "");
        return name.Contains(_preferredProduct, StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadAxis(JoystickState s, string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("rot") && n.StartsWith("x")) return s.RotationX;
        if (n.Contains("rot") && n.StartsWith("y")) return s.RotationY;
        if (n.Contains("rot") && n.StartsWith("z")) return s.RotationZ;
        if (n.StartsWith("x")) return s.X;
        if (n.StartsWith("y")) return s.Y;
        if (n.StartsWith("z")) return s.Z;
        if (n.Contains("slider")) return s.Sliders.Length > 0 ? s.Sliders[0] : s.X;
        if (n.Contains("dial")) return s.Sliders.Length > 1 ? s.Sliders[1] : s.X;
        return s.X;
    }

    private void SetDevice(string name)
    {
        if (DeviceName == name) return;
        DeviceName = name;
        Changed?.Invoke();
    }

    private void DropDevice()
    {
        try { _device?.Unacquire(); } catch { }
        try { _device?.Dispose(); } catch { }
        try { _di?.Dispose(); } catch { }
        _device = null;
        _di = null;
        SetDevice("No wheel");
    }

    public void Dispose()
    {
        _running = false;
        try { _thread?.Join(500); } catch { }
        DropDevice();
        TimeEndPeriod(1);
    }
}
