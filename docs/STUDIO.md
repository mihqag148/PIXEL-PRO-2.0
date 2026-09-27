# Studio Windows 2.5

## Layout

**Left**
- Profile & Key Selection: 25 profiles × 8 keys.
- 5×5 profile selector.
- key icon preview / delete icon / per-key color.
- Device List: Refresh, Auto Find, Connect, Disconnect, Read, Sync / Save.
- right-click Device List for connect, RGB, sync, read, theme, firmware and disconnect actions.

**Right**
- Getting Started.
- Key Configuration.
- Plugins.
- Display & Media.
- Auto Profile.
- Log.

Header: Import/Export full preset, Import/Export Profile, Presets Gallery, firmware page, Light/Dark, language selector.

## Key Configuration

Action toolbox → ordered Action Sequence → Action Parameters.

Supported actions include:
Website, Launch App, Open Folder, Open File, Text, Shortcut, Wait, Mouse Move, Mouse Click, Wheel, Media, Change Profile, Functional Key, Device Control, Power Off Computer and Script.

Tối đa **512 actions/key**.

## HID Mode

Native without Studio:
- keyboard shortcut / functional key
- text
- wait
- mouse click/wheel
- media
- change profile
- profile next/previous
- supported Script sequences compiled to PXS2

Windows-only actions use App Mode. App Mode stays alive when Studio is minimized/closed to system tray.

## Save / copy / import

- SAVE KEY: selected key.
- SAVE PROFILE: eight keys in selected profile.
- SAVE TO DEVICE: all 25×8 bindings + device settings.
- drag key → key to copy.
- drag profile → profile to copy.
- Import/Export Profile uses `*.profile.json`.
- Full preset schema is 4; schema 2/3 five-profile presets auto-migrate.

## Presets Gallery

Built-in presets include Windows Essentials, Media Control, Browser, Office, OBS Studio, Fusion 360, DaVinci Resolve and Photoshop.

Gallery supports:
- search
- category
- Filter Duplicated
- local presets
- import downloaded preset
- open local Presets folder

Locations:
- Presets: `%LOCALAPPDATA%\PixelPro2\presets`
- Exports: `%LOCALAPPDATA%\PixelPro2\exports`
- current draft: `%LOCALAPPDATA%\PixelPro2\preset-v4.json`

## Icons

Select Icon File works while offline. Studio stores the icon locally, previews it on K1…K8, and uploads it through Save Key/Profile/Device.

Media Control generates a matching local icon automatically for Play/Pause, Next, Previous, Stop, Volume Up/Down and Mute.

## Plugins

Studio main process does not load LibreHardwareMonitor.

Named pipe:
```text
PIXEL_PRO_2_PLUGINS
```

Bundled plugin executables:
- `PixelPro2.PcMonitorPlugin.exe`
- `PixelPro2.MusicPlugin.exe`

PC Monitor reads hardware telemetry in its own process. Music plugin reads Windows SMTC in its own process. Studio receives their JSON messages and forwards the selected full-screen mode to the active device.

The Plugins tab can enable/disable:
- Named Pipe Service
- PC Monitoring Plugin
- Music Player Plugin (SMTC)

Only one full-screen display plugin is active on the device at a time; HID keys remain active.

## Multi-device

Auto Find can keep multiple PIXEL PRO COM sessions connected. Selecting a connected device changes the active device without closing the other sessions.

## Auto Sync

Firmware exposes deterministic FNV-1a `KEYHASH` over all 25×8 bindings. On connect:
- YES: PC → device
- NO: device → PC
- CANCEL: postpone

## Auto Profile

Foreground Windows process rules can map applications to profiles P1…P25.

## Touch affine

Raw hardware mapping:
```text
rawX <- D13 (YP)
rawY <- D14 (XM)
screenX <- rawY reversed
screenY <- rawX
```

Filtering:
- 3 coordinate reads per poll.
- normal stability spread <=100; calibration <=180.
- DOWN requires 3 close polls.
- UP requires 4 misses.
- poll interval 20 ms.
- calibration uses median samples + four-point affine solve.

Touch Diagnostics streams:
`E|TOUCHRAW|pressed|rawX|rawY|quality|mappedX|mappedY`.

## USB Safe Mode

PIXEL PRO exposes HID + CDC only; no USB mass-storage interface is exposed. The UI shows Safe Mode as permanently ON.

## Background behavior

Minimize or normal window close sends Studio to the Windows notification area instead of stopping App Mode/plugins. Use the tray menu → Exit to terminate Studio and its bundled child plugins.
