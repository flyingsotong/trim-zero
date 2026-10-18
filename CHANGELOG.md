# Changelog

## 0.2.0 - unreleased

- Tip jar link in the settings footer, next to the version. It opens
  https://magentadebrief.com/donate/ in the default browser.
- Direction and the takeoff datum are remembered per aircraft. Switching to a known
  aircraft restores its datum; the aircraft's live trim is still the control base, so
  the first wheel movement cannot snap.
- The version in the settings footer and the feedback subject comes from the build.

## 0.1.0 - 18 October 2026

First public build.

- One 400 x 632 window: trim dial, ZERO, NORMAL/INVERTED direction switch, minimize,
  feedback link.
- Reads one DirectInput axis at about 250 Hz off the UI thread; writes elevator trim at
  50 Hz.
- Writes `ELEVATOR TRIM POSITION` relative to the sim's live value. Never writes the
  wheel's absolute angle.
- ZERO records the wheel count and aircraft trim; it does not move the aircraft.
- Two gains either side of the mark, from a measured two-point calibration.
- Drift between the aircraft and the wheel datum, shown on the dial and in the meters.
- Autopilot clutch: wheel held while `AUTOPILOT MASTER` is on; reseeds on disconnect.
- Direction remembered per aircraft; window position remembered; always-on-top setting.
- Per-user settings at `%LOCALAPPDATA%\TrimZero\settings.json`; no network, no telemetry,
  no updater.
- Fixed per-side gains and the first-run `PRESS ZERO AT THE MARK` prompt.
