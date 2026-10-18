namespace TrimZero.Core;

/// <summary>
/// The whole trim model. The wheel is a relative control: movement is applied to
/// the aircraft's current trim, never to an absolute axis value, so nothing can
/// snap. The datum (zero) only changes what the app believes the wheel's position
/// means; it never moves the aircraft.
/// </summary>
public sealed class TrimEngine
{
    private readonly object _gate = new();

    private double _wheelMin = 0;
    private double _wheelMax = 65535;
    private double _sensitivity = 1.0;
    private bool _invert;

    private int _lastCounts;
    private bool _hasCounts;

    private double _simPct = 50;
    private bool _connected;
    private bool _ap;

    private double _predicted = 50;
    private double? _pending;
    private long _lastMoveMs;

    private bool _hasZero;
    private double _zeroWheel;
    private double _zeroTrim = 50;
    private double _takeoffPct = -1;

    public double WheelMin { get { lock (_gate) return _wheelMin; } }
    public double WheelMax { get { lock (_gate) return _wheelMax; } }
    public double Sensitivity { get { lock (_gate) return _sensitivity; } }
    public bool Invert { get { lock (_gate) return _invert; } set { lock (_gate) _invert = value; } }

    public void Configure(double wheelMin, double wheelMax, double sensitivity, bool invert)
    {
        lock (_gate)
        {
            if (wheelMax > wheelMin + 500)
            {
                _wheelMin = wheelMin;
                _wheelMax = wheelMax;
            }
            _sensitivity = Math.Clamp(sensitivity, 0.2, 3.0);
            _invert = invert;
        }
    }

    public void SetTravel(double wheelMin, double wheelMax)
    {
        lock (_gate)
        {
            if (wheelMax > wheelMin + 500)
            {
                _wheelMin = wheelMin;
                _wheelMax = wheelMax;
            }
        }
    }

    public void SetSensitivity(double sensitivity)
    {
        lock (_gate) _sensitivity = Math.Clamp(sensitivity, 0.2, 3.0);
    }

    public void SetInvert(bool invert)
    {
        lock (_gate) _invert = invert;
    }

    /// <summary>Forget the last physical sample, e.g. after a device reconnect.</summary>
    public void ResetWheel()
    {
        lock (_gate)
        {
            _hasCounts = false;
            _pending = null;
        }
    }

    public void OnWheel(int counts)
    {
        lock (_gate)
        {
            if (!_hasCounts)
            {
                _lastCounts = counts;
                _hasCounts = true;
                return;
            }

            int delta = counts - _lastCounts;
            if (delta == 0) return;
            if (Math.Abs(delta) < 2) return; // let slow motion accumulate; ignore sensor jitter
            _lastCounts = counts;

            _lastMoveMs = Environment.TickCount64;
            if (!_connected || _ap) return;

            if (_invert) delta = -delta;

            // The base is our last commanded value, not a fresh read: this keeps a
            // 200 Hz wheel ahead of a 60 Hz sim without waiting for a round trip.
            double basePct = _hasZero ? _predicted : _simPct;
            double target = Math.Clamp(basePct + delta * GainForSide(_lastCounts), 0, 100);
            _predicted = target;
            _pending = target;
        }
    }

    public void OnSim(double pct, bool ap, bool connected)
    {
        lock (_gate)
        {
            pct = Math.Clamp(pct, 0, 100);
            bool wasConnected = _connected;
            bool wasAp = _ap;

            _simPct = pct;
            _connected = connected;
            _ap = ap;

            if (!connected) return;

            if (!wasConnected)
            {
                // First frame after connect: adopt the aircraft's trim so the first
                // wheel movement cannot jump. That is a datum, not a takeoff mark.
                _predicted = pct;
                ZeroLocked(markTakeoff: false);
                return;
            }

            if (wasAp && !ap)
            {
                // Autopilot released: the aircraft trimmed itself while it flew.
                // Reseed the control base, but keep the takeoff datum so the box's
                // mark is not silently invalidated. The drift is shown; ZERO re-datums.
                _predicted = pct;
                return;
            }

            if (!_ap && Environment.TickCount64 - _lastMoveMs > 120)
            {
                _predicted = pct;
            }
        }
    }

    public void Zero()
    {
        lock (_gate) ZeroLocked(markTakeoff: true);
    }

    public void SetTakeoffRef(double pct)
    {
        lock (_gate) _takeoffPct = pct is >= 0 and <= 100 ? pct : -1;
    }

    /// <summary>Adopt the aircraft's trim after a flight or aircraft change.</summary>
    public void ResetToAircraft()
    {
        lock (_gate)
        {
            _predicted = _simPct;
            _pending = null;
            ZeroLocked(markTakeoff: false);
        }
    }

    private void ZeroLocked(bool markTakeoff)
    {
        _predicted = _simPct;
        _pending = null;
        if (!_hasCounts) return;
        _zeroWheel = _lastCounts;
        _zeroTrim = _simPct;
        if (markTakeoff) _takeoffPct = _simPct;
        _hasZero = true;
    }

    public double? TakePending()
    {
        lock (_gate)
        {
            var value = _pending;
            _pending = null;
            return value;
        }
    }

    public Snapshot GetSnapshot()
    {
        lock (_gate)
        {
            double ghost = _hasZero
                ? Math.Clamp(_zeroTrim + (_lastCounts - _zeroWheel) * GainForSide(_lastCounts) * (_invert ? -1 : 1), 0, 100)
                : _simPct;

            return new Snapshot(
                SimPct: _simPct,
                GhostPct: ghost,
                Drift: ghost - _simPct,
                Connected: _connected,
                Ap: _ap,
                HasZero: _hasZero,
                WheelCounts: _lastCounts,
                TakeoffPct: _takeoffPct);
        }
    }

    /// <summary>
    /// Trim units per wheel count. Once the wheel has a takeoff datum, each side of
    /// the datum gets its own gain, so the limited physical travel still reaches the
    /// aircraft's full up and down limits. That is the "handled digitally" part:
    /// the box's arc is short, the trim range is not.
    /// </summary>
    private double GainForSide(int counts)
    {
        double span = Math.Max(1000, _wheelMax - _wheelMin);
        double defaultGain = 100.0 / span;

        if (!_hasZero || _zeroWheel <= _wheelMin + 1 || _zeroWheel >= _wheelMax - 1)
            return defaultGain * _sensitivity;

        bool up = counts >= _zeroWheel;
        double physical = up ? _wheelMax - _zeroWheel : _zeroWheel - _wheelMin;
        double trim = up ? 100.0 - _zeroTrim : _zeroTrim;
        if (physical < 100) return defaultGain * _sensitivity;

        return Math.Max(0.00001, trim / physical) * _sensitivity;
    }

    public readonly record struct Snapshot(
        double SimPct,
        double GhostPct,
        double Drift,
        bool Connected,
        bool Ap,
        bool HasZero,
        int WheelCounts,
        double TakeoffPct);
}
