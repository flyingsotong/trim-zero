using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.FlightSimulator.SimConnect;

namespace TrimZero.Services;

public readonly record struct SimState(double Pct, bool ApMaster, bool Connected, string Aircraft);

/// <summary>
/// Owns the SimConnect conversation. The writable variable is
/// ELEVATOR TRIM POSITION in radians: measured live, ELEVATOR TRIM PCT is not
/// applied by the C172. The cockpit percentage is derived from the position and
/// the aircraft's own neutral and up/down limits, so no C172 constant is in the
/// path; the app then works in a 0-100 travel scale.
/// </summary>
public sealed class SimConnectService : IDisposable
{
    private const int WmUserSimConnect = 0x0402;
    private const uint UserObject = 0;

    private enum Definition { State = 0, WritePos = 1, ApMaster = 2, OnGround = 3, Title = 4 }
    private enum Request { State = 0, ApMaster = 1, OnGround = 2, Title = 3 }
    private enum Event { SimStart = 0, SimStop = 1 }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TrimState
    {
        public double Position;   // radians
        public double Neutral;    // radians
        public double UpLimit;    // degrees
        public double DownLimit;  // degrees
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DoubleValue { public double Value; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct IntValue { public int Value; }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    private struct StringValue
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Value;
    }

    private SimConnect? _sc;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _ap;
    private bool _connected;
    private bool _hasTrim;
    private bool _hasLimits;
    private double _position;
    private double _minRad;
    private double _maxRad;
    private string _aircraft = "";

    public event Action<SimState>? StateChanged;

    public bool IsConnected => _connected;

    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(Hook);
    }

    public void EnsureConnected()
    {
        if (_sc is not null || _hwnd == IntPtr.Zero) return;

        try
        {
            _sc = new SimConnect("Trim Zero", _hwnd, WmUserSimConnect, null, 0);
            _sc.OnRecvOpen += OnOpen;
            _sc.OnRecvQuit += OnQuit;
            _sc.OnRecvException += OnException;
            _sc.OnRecvSimobjectData += OnData;
            _sc.OnRecvEvent += OnEvent;
        }
        catch
        {
            _sc = null;
        }
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmUserSimConnect && _sc is not null)
        {
            try
            {
                _sc.ReceiveMessage();
            }
            catch
            {
                Disconnect();
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        if (_sc is null) return;

        _sc.SubscribeToSystemEvent(Event.SimStart, "SimStart");
        _sc.SubscribeToSystemEvent(Event.SimStop, "SimStop");
        _sc.SetSystemEventState(Event.SimStart, SIMCONNECT_STATE.ON);
        _sc.SetSystemEventState(Event.SimStop, SIMCONNECT_STATE.ON);

        var sc = _sc;
        sc.AddToDataDefinition(Definition.State, "ELEVATOR TRIM POSITION", "Radians", SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(Definition.State, "ELEVATOR TRIM NEUTRAL", "Radians", SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(Definition.State, "ELEVATOR TRIM UP LIMIT", "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(Definition.State, "ELEVATOR TRIM DOWN LIMIT", "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);

        sc.AddToDataDefinition(Definition.WritePos, "ELEVATOR TRIM POSITION", "Radians", SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(Definition.ApMaster, "AUTOPILOT MASTER", "Bool", SIMCONNECT_DATATYPE.INT32, 0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(Definition.OnGround, "SIM ON GROUND", "Bool", SIMCONNECT_DATATYPE.INT32, 0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(Definition.Title, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0f, SimConnect.SIMCONNECT_UNUSED);

        sc.RegisterDataDefineStruct<TrimState>(Definition.State);
        sc.RegisterDataDefineStruct<DoubleValue>(Definition.WritePos);
        sc.RegisterDataDefineStruct<IntValue>(Definition.ApMaster);
        sc.RegisterDataDefineStruct<IntValue>(Definition.OnGround);
        sc.RegisterDataDefineStruct<StringValue>(Definition.Title);

        RequestData();

        _connected = true;
        // No state is published until the first real trim frame. Publishing an
        // invented value here would become the datum before the aircraft is read.
    }

    private void RequestData()
    {
        if (_sc is null) return;
        _sc.RequestDataOnSimObject(Request.State, Definition.State, UserObject, SIMCONNECT_PERIOD.SIM_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        _sc.RequestDataOnSimObject(Request.ApMaster, Definition.ApMaster, UserObject, SIMCONNECT_PERIOD.SIM_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        _sc.RequestDataOnSimObject(Request.OnGround, Definition.OnGround, UserObject, SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        // RequestDataOnSimObjectType never delivered the title on MSFS 2024;
        // a one-shot data-on-object request does.
        _sc.RequestDataOnSimObject(Request.Title, Definition.Title, UserObject, SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 1);
    }

    private void OnQuit(SimConnect sender, SIMCONNECT_RECV data) => Disconnect();

    private void OnException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        // SimConnect reports a data error on the combined trim definition on some
        // writes even when the position is applied. The applied value is verified
        // by the next state frame, which is the only authority that matters here.
    }

    private void OnEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
    {
        if (data.uEventID == (uint)Event.SimStart)
        {
            _connected = true;
            _hasTrim = false;
            _ap = false;
            RequestData();
        }
        else if (data.uEventID == (uint)Event.SimStop)
        {
            _connected = false;
            Raise();
        }
    }

    private void OnData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        switch ((Definition)data.dwDefineID)
        {
            case Definition.State:
                var state = (TrimState)data.dwData[0];
                _position = state.Position;
                double min = state.Neutral - state.DownLimit * Math.PI / 180.0;
                double max = state.Neutral + state.UpLimit * Math.PI / 180.0;
                if (max - min > 0.01)
                {
                    _minRad = min;
                    _maxRad = max;
                    _hasLimits = true;
                }
                _hasTrim = true;
                Raise();
                break;

            case Definition.ApMaster:
                _ap = ((IntValue)data.dwData[0]).Value != 0;
                if (_hasTrim) Raise();
                break;

            case Definition.Title:
                _aircraft = (((StringValue)data.dwData[0]).Value ?? "").Trim('\0', ' ');
                if (_hasTrim) Raise();
                break;
        }
    }

    /// <summary>The aircraft's trim as 0-100 of its full travel.</summary>
    public double Pct
    {
        get
        {
            if (!_hasLimits) return 50;
            double value = (_position - _minRad) / (_maxRad - _minRad) * 100.0;
            return Math.Clamp(value, 0, 100);
        }
    }

    public void SetTrimPct(double pct)
    {
        if (_sc is null || !_hasLimits) return;
        pct = Math.Clamp(pct, 0, 100);
        double position = _minRad + pct / 100.0 * (_maxRad - _minRad);
        try
        {
            _sc.SetDataOnSimObject(Definition.WritePos, UserObject, SIMCONNECT_DATA_SET_FLAG.DEFAULT,
                new DoubleValue { Value = position });
        }
        catch
        {
            Disconnect();
        }
    }

    private void Raise() => StateChanged?.Invoke(new SimState(Pct, _ap, _connected, _aircraft));

    private void Disconnect()
    {
        _connected = false;
        _hasTrim = false;
        _hasLimits = false;
        try { _sc?.Dispose(); } catch { }
        _sc = null;
        Raise();
    }

    public void Dispose()
    {
        _source?.RemoveHook(Hook);
        _source = null;
        Disconnect();
    }
}
