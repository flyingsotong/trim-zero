using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TrimZero.Core;
using TrimZero.Services;

namespace TrimZero.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly Brush MutedBrush = Freeze(Color.FromRgb(0x8A, 0x91, 0x9B));
    private static readonly Brush AmberBrush = Freeze(Color.FromRgb(0xF2, 0xB2, 0x3E));
    private static readonly Brush GreenBrush = Freeze(Color.FromRgb(0x63, 0xD2, 0x9B));
    private static readonly Brush RedBrush = Freeze(Color.FromRgb(0xE4, 0x5B, 0x4F));

    private readonly TrimEngine _engine = new();
    private readonly WheelService _wheel = new();
    private readonly DispatcherTimer _timer;
    private readonly bool _demo;
    private readonly bool _nudge;
    private bool _nudgeStarted;
    private bool _nudgeUp;
    private bool _nudgeDown;
    private bool _nudgeDone;
    private long _nudgeStart;
    private readonly string _nudgeLog = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "trimzero-nudge.txt");
    private AppSettings _settings;
    private SimConnectService? _sim;
    private string _aircraft = "";
    private bool _demoReady;
    private bool _disposed;
    private int _tick;

    // Backing fields for bindings.
    private double _simPct;
    private double _ghostPct;
    private double _drift;
    private bool _hasZero;
    private bool _zeroedThisSession;
    private double _takeoffPct = -1;
    private bool _isConnected;
    private bool _apOn;
    private string _statusText = "WAITING FOR MSFS";
    private Brush _statusBrush = MutedBrush;
    private string _aircraftText = "NO AIRCRAFT";
    private string _deviceText = "NO WHEEL";
    private bool _isSettingsOpen;
    private bool _invert;
    private bool _topmost = true;
    private double _sensitivity = 1.0;
    private double _wheelMin;
    private double _wheelMax = 65535;
    private string _wheelNowText = "—";

    public MainViewModel(bool demo, bool nudge = false)
    {
        _demo = demo;
        _nudge = nudge;
        _settings = SettingsStore.Load();
        _engine.Configure(_settings.WheelMin, _settings.WheelMax, _settings.Sensitivity, _settings.Invert);
        _invert = _settings.Invert;
        _topmost = _settings.Topmost;
        _sensitivity = _settings.Sensitivity;
        _wheelMin = _settings.WheelMin;
        _wheelMax = _settings.WheelMax;

        _wheel.Sample += _engine.OnWheel;
        _wheel.Changed += OnWheelChanged;

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Render, Pump, Dispatcher.CurrentDispatcher);
    }

    public void Initialize(IntPtr hwnd)
    {
        if (_demo)
        {
            _engine.OnWheel(36053);
            DeviceText = "MEZA TRIM · X AXIS";
            _aircraft = "CESSNA 172 SKYHAWK";
            AircraftText = _aircraft;
        }
        else
        {
            _sim = new SimConnectService();
            _sim.StateChanged += OnSimState;
            _sim.Initialize(hwnd);
            if (_nudge) _engine.OnWheel(36053);
            else _wheel.Start(hwnd, _settings.DeviceProduct, _settings.AxisName);
        }

        _timer.Start();
    }

    private void OnWheelChanged()
    {
        _engine.ResetWheel();
        Application.Current.Dispatcher.BeginInvoke(new Action(RefreshDevice));
    }

    private void RefreshDevice()
    {
        DeviceText = _wheel.DeviceName == "No wheel"
            ? "NO WHEEL"
            : $"{_wheel.DeviceName.ToUpperInvariant()} · {_wheel.AxisName.ToUpperInvariant()}";
        OnPropertyChanged(nameof(HasMultipleAxes));
        OnPropertyChanged(nameof(CanZero));
        OnPropertyChanged(nameof(DatumChipVisibility));
    }

    public bool HasMultipleAxes => _wheel.AxisNames.Count > 1;
    public bool CanZero => _isConnected && (_demo || _wheel.IsConnected);

    private void OnSimState(SimState state)
    {
        bool wasConnected = _isConnected;
        _engine.OnSim(state.Pct, state.ApMaster, state.Connected);
        IsConnected = state.Connected;
        ApOn = state.ApMaster;

        if (!wasConnected && state.Connected)
        {
            _zeroedThisSession = _engine.GetSnapshot().HasTakeoffDatum;
            NotifyDatumChip();
        }

        if (!string.IsNullOrWhiteSpace(state.Aircraft) &&
            !string.Equals(state.Aircraft, _aircraft, StringComparison.OrdinalIgnoreCase))
        {
            _aircraft = state.Aircraft;
            AircraftText = Shorten(_aircraft, 34).ToUpperInvariant();
            _zeroedThisSession = false;
            if (TryGetAircraftInvert(_aircraft, out bool saved))
                ApplyInvert(saved, remember: false);

            if (TryGetAircraftTakeoff(_aircraft, out double takeoff) &&
                _settings.MarkWheel is double mark)
            {
                _engine.RestoreDatum(takeoff, mark);
                _zeroedThisSession = true;
            }
            else
            {
                _engine.ResetToAircraft();
            }

            NotifyDatumChip();
        }
    }

    private void Pump(object? sender, EventArgs e)
    {
        _tick++;

        if (_demo)
        {
            if (!_demoReady)
            {
                _engine.OnSim(58, false, true);
                _engine.Zero();
                _engine.OnSim(62, false, true);
                _zeroedThisSession = true;
                NotifyDatumChip();
                _demoReady = true;
            }
        }
        else if (_sim is not null)
        {
            if (_tick % 100 == 0) _sim.EnsureConnected();

            var target = _engine.TakePending();
            if (target is double value) _sim.SetTrimPct(value);
        }

        var snapshot = _engine.GetSnapshot();
        SimPct = snapshot.SimPct;
        GhostPct = snapshot.GhostPct;
        HasZero = snapshot.HasZero;
        Drift = snapshot.Drift;
        TakeoffPct = snapshot.TakeoffPct;
        WheelNowText = snapshot.WheelCounts.ToString("N0");
        IsConnected = snapshot.Connected;
        ApOn = snapshot.Ap;

        if (_nudge) RunNudge(snapshot);

        if (!snapshot.Connected)
        {
            StatusText = _demo ? "DEMO" : "WAITING FOR MSFS";
            StatusBrush = MutedBrush;
        }
        else if (!_demo && !_wheel.IsConnected)
        {
            StatusText = "NO WHEEL";
            StatusBrush = AmberBrush;
        }
        else if (snapshot.Ap)
        {
            StatusText = "AUTOPILOT · WHEEL HELD";
            StatusBrush = AmberBrush;
        }
        else if (snapshot.HasZero && Math.Abs(snapshot.Drift) > 8.0)
        {
            StatusText = "WHEEL OFF DATUM";
            StatusBrush = RedBrush;
        }
        else if (snapshot.HasZero && Math.Abs(snapshot.Drift) > 1.0)
        {
            StatusText = "WHEEL OFF DATUM";
            StatusBrush = AmberBrush;
        }
        else
        {
            StatusText = "READY";
            StatusBrush = GreenBrush;
        }
    }

    public void Zero()
    {
        _engine.Zero();
        var snapshot = _engine.GetSnapshot();
        if (!snapshot.HasTakeoffDatum) return;

        _zeroedThisSession = true;
        _settings.MarkWheel = snapshot.WheelCounts;
        if (!string.IsNullOrWhiteSpace(_aircraft))
            _settings.AircraftTakeoff[_aircraft] = snapshot.TakeoffPct;
        Save();
        NotifyDatumChip();
    }

    private void NotifyDatumChip()
    {
        OnPropertyChanged(nameof(DatumChipText));
        OnPropertyChanged(nameof(DatumChipBrush));
    }

    private void RunNudge(TrimEngine.Snapshot s)
    {
        if (!_nudgeStarted)
        {
            if (!s.Connected || !s.HasZero) return;
            _nudgeStarted = true;
            _nudgeStart = Environment.TickCount64;
            Log($"start sim={s.SimPct:F2} wheel={s.WheelCounts}");
            return;
        }

        long ms = Environment.TickCount64 - _nudgeStart;
        if (!_nudgeUp && ms > 2000)
        {
            Log($"before up sim={s.SimPct:F2}");
            _engine.OnWheel(36053 + 4000);
            _nudgeUp = true;
        }
        else if (_nudgeUp && !_nudgeDown && ms > 4500)
        {
            Log($"after up sim={s.SimPct:F2}");
            _engine.OnWheel(36053 - 4000);
            _nudgeDown = true;
        }
        else if (_nudgeDown && !_nudgeDone && ms > 7000)
        {
            Log($"after down sim={s.SimPct:F2}");
            _nudgeDone = true;
        }
    }

    private void Log(string line)
    {
        try { System.IO.File.AppendAllText(_nudgeLog, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}"); }
        catch { }
    }

    public void SetTravelDown()
    {
        var counts = _engine.GetSnapshot().WheelCounts;
        if (counts < _settings.WheelMax - 500)
        {
            _settings.WheelMin = counts;
            WheelMin = counts;
            _engine.SetTravel(_settings.WheelMin, _settings.WheelMax);
            Save();
        }
    }

    public void SetTravelUp()
    {
        var counts = _engine.GetSnapshot().WheelCounts;
        if (counts > _settings.WheelMin + 500)
        {
            _settings.WheelMax = counts;
            WheelMax = counts;
            _engine.SetTravel(_settings.WheelMin, _settings.WheelMax);
            Save();
        }
    }

    public void SetSpeed(double value)
    {
        Sensitivity = value;
        _settings.Sensitivity = value;
        _engine.SetSensitivity(value);
        Save();
        OnPropertyChanged(nameof(SensitivitySlow));
        OnPropertyChanged(nameof(SensitivityNormal));
        OnPropertyChanged(nameof(SensitivityFast));
        OnPropertyChanged(nameof(SpeedText));
    }

    public void CycleSpeed()
    {
        double next = _sensitivity < 0.8 ? 1.0 : _sensitivity < 1.3 ? 1.6 : 0.6;
        SetSpeed(next);
    }

    public string SpeedText => Math.Abs(_sensitivity - 0.6) < 0.01 ? "SLOW"
        : Math.Abs(_sensitivity - 1.6) < 0.01 ? "FAST" : "NORMAL";

    public void CycleAxis()
    {
        _wheel.CycleAxis();
        _settings.AxisName = _wheel.AxisName;
        RefreshDevice();
        Save();
    }

    public bool SensitivitySlow => Math.Abs(_sensitivity - 0.6) < 0.01;
    public bool SensitivityNormal => Math.Abs(_sensitivity - 1.0) < 0.01;
    public bool SensitivityFast => Math.Abs(_sensitivity - 1.6) < 0.01;

    private void Save() => SettingsStore.Save(_settings);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _wheel.Dispose();
        _sim?.Dispose();
    }

    private static string Shorten(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public bool IsSettingsOpen { get => _isSettingsOpen; set => Set(ref _isSettingsOpen, value); }
    public bool Topmost
    {
        get => _topmost;
        set
        {
            if (!Set(ref _topmost, value)) return;
            _settings.Topmost = value;
            Save();
        }
    }

    public bool Invert
    {
        get => _invert;
        set => ApplyInvert(value, remember: true);
    }

    private void ApplyInvert(bool value, bool remember)
    {
        Set(ref _invert, value);
        _engine.SetInvert(value);
        if (!remember) return;

        _settings.Invert = value;
        if (!string.IsNullOrWhiteSpace(_aircraft))
            _settings.AircraftInvert[_aircraft] = value;
        Save();
    }

    private bool TryGetAircraftInvert(string title, out bool value)
    {
        foreach (var pair in _settings.AircraftInvert)
        {
            if (string.Equals(pair.Key, title, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }
        value = false;
        return false;
    }

    private bool TryGetAircraftTakeoff(string title, out double value)
    {
        foreach (var pair in _settings.AircraftTakeoff)
        {
            if (string.Equals(pair.Key, title, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }
        value = 0;
        return false;
    }

    public string VersionText { get; } =
        "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");

    public double? WindowLeft => _settings.WindowLeft;
    public double? WindowTop => _settings.WindowTop;

    public void SaveWindowPosition(double left, double top)
    {
        _settings.WindowLeft = left;
        _settings.WindowTop = top;
        Save();
    }

    public double Sensitivity { get => _sensitivity; private set => Set(ref _sensitivity, value); }
    public double WheelMin { get => _wheelMin; private set { if (Set(ref _wheelMin, value)) OnPropertyChanged(nameof(WheelMinText)); } }
    public double WheelMax { get => _wheelMax; private set { if (Set(ref _wheelMax, value)) OnPropertyChanged(nameof(WheelMaxText)); } }
    public string WheelMinText => $"{_wheelMin:0}";
    public string WheelMaxText => $"{_wheelMax:0}";
    public string WheelNowText { get => _wheelNowText; private set => Set(ref _wheelNowText, value); }

    public double SimPct
    {
        get => _simPct;
        private set
        {
            if (!Set(ref _simPct, value)) return;
            OnPropertyChanged(nameof(SimText));
        }
    }

    public string SimText => _isConnected || _demo ? _simPct.ToString("0.0") : "—";
    public string WheelText => _hasZero ? _ghostPct.ToString("0.0") : "—";
    public Visibility NumberVisibility => _isConnected || _demo ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OfflineVisibility => _isConnected || _demo ? Visibility.Collapsed : Visibility.Visible;
    public double GhostPct
    {
        get => _ghostPct;
        private set
        {
            if (!Set(ref _ghostPct, value)) return;
            OnPropertyChanged(nameof(WheelText));
        }
    }
    public double Drift
    {
        get => _drift;
        private set
        {
            if (!Set(ref _drift, value)) return;
            OnPropertyChanged(nameof(DriftText));
            OnPropertyChanged(nameof(DriftBrush));
        }
    }

    public string DriftText => !_hasZero ? "—" : $"{_drift:+0.0;-0.0;0.0}%";
    public Brush DriftBrush => Math.Abs(_drift) > 8.0 ? RedBrush : Math.Abs(_drift) > 1.0 ? AmberBrush : MutedBrush;
    public bool HasZero
    {
        get => _hasZero;
        private set
        {
            if (!Set(ref _hasZero, value)) return;
            OnPropertyChanged(nameof(DriftText));
            OnPropertyChanged(nameof(WheelText));
        }
    }
    public double TakeoffPct
    {
        get => _takeoffPct;
        private set
        {
            if (!Set(ref _takeoffPct, value)) return;
            OnPropertyChanged(nameof(HasTakeoff));
        }
    }

    public bool HasTakeoff
    {
        get => _takeoffPct >= 0;
    }

    public string DatumChipText => _zeroedThisSession ? "ZEROED" : "PRESS ZERO AT THE MARK";
    public Brush DatumChipBrush => _zeroedThisSession ? GreenBrush : AmberBrush;
    public Visibility DatumChipVisibility => CanZero ? Visibility.Visible : Visibility.Collapsed;
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (!Set(ref _isConnected, value)) return;
            OnPropertyChanged(nameof(SimText));
            OnPropertyChanged(nameof(NumberVisibility));
            OnPropertyChanged(nameof(OfflineVisibility));
            OnPropertyChanged(nameof(CanZero));
            OnPropertyChanged(nameof(DatumChipVisibility));
        }
    }
    public bool ApOn { get => _apOn; private set => Set(ref _apOn, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public Brush StatusBrush { get => _statusBrush; private set => Set(ref _statusBrush, value); }
    public string AircraftText { get => _aircraftText; private set => Set(ref _aircraftText, value); }
    public string DeviceText { get => _deviceText; private set => Set(ref _deviceText, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
