# Changelog

## 0.2.2 - 27 October 2026

- ZERO only records a takeoff datum on the ground. In flight it re-datums the wheel and
  leaves the takeoff band and the saved mark alone, so pressing it in cruise can no
  longer overwrite the datum taken at the mark. Before this, an in-flight ZERO silently
  replaced the takeoff trim and the mark's count with wherever the autopilot had left
  the aircraft. The app now reads `SIM ON GROUND`; the definition and request were
  already there, but nothing handled the frame.
- The gain for each side of the datum is chosen from the axis direction. With the wheel
  inverted, a turn toward nose-up lowers the count, so the opposite side's gain was
  used, about a third wrong.
- The direction switch no longer reports its change under the wrong property name, so
  it cannot show one state while the engine runs the other.
- Trim speed is 0.6 / 0.8 / 1.0. The old 1.6 preset reached the trim limit two thirds
  of the way round the wheel and left the rest of the travel dead. A saved value above
  1.0 is clamped on load.
- A travel SET pressed at the wrong end now says so instead of failing silently. The
  guards were correct but invisible, and the default counts look plausible, so a
  rejected press read as an already-set value.
- Tooltips draw on a dark surface. The theme template ignores `Background`, and the
  global TextBlock foreground is near-white, so every tooltip was a blank light box.
- Esc closes the settings panel and returns to the main screen.
- Magenta Debrief maker's mark in the settings About section, linking to
  https://magentadebrief.com. The app's instrument colours are unchanged.
- README footer with the Magenta lockup in light and dark variants; the Magenta marks
  are recorded as trademarks, not MIT.

## 0.2.1 - 19 October 2026

- Device auto-detect now only picks a device whose name contains "Trim". A Meza
  Switch Panel or a joystick no longer wins by containing "Meza". Devices with no axes
  are rejected, so a button-only panel cannot be read as a constant 0. Changing the
  device or axis clears the saved takeoff datum, because the mark is a count on that
  axis.
- A new aircraft no longer says WHEEL OFF DATUM before the first ZERO. Drift, the
  wheel figure and the ghost tick stay hidden until a takeoff datum exists; the status
  reads READY and the dial prompts PRESS ZERO AT THE MARK.
- Start with Windows (minimized) setting. It waits for MSFS; no service, no admin, no
  tray. A second launch restores the running window instead of only showing a message.

## 0.2.0 - 19 October 2026

- Device picker for any DirectInput analogue wheel, not only the Meza. A device named
  Meza or Trim is auto-detected; anything else is selected under DEVICE in settings.
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
