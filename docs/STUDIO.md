# Studio Windows 2.4

## Layout

**Left**
- Profile & Key Selection: 25 profiles × 8 keys.
- 5×5 profile selector.
- key icon / delete icon / per-key color.
- Device List: Refresh, Auto Find, Connect, Disconnect, Read, Sync / Save.

**Right**
- Getting Started.
- Key Configuration: Action toolbox, ordered Action Sequence, parameters.
- Display & Media.
- Auto Profile.
- Log.

Header: Import/Export full preset, Import/Export Profile, firmware page, Light/Dark.

## Actions

Website, Launch App, Open Folder, Open File, Text, Shortcut, Wait, Mouse Move, Mouse Click, Wheel, Media, Change Profile, Functional Key, Device Control, Power Off Computer.

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

Longer native sequences compile to **PXS2** and are stored in SPIFFS. Windows-only actions switch the key to App Mode.

## Save / copy / import

- SAVE KEY: selected key only.
- SAVE PROFILE: eight keys in selected profile.
- SAVE TO DEVICE: all 25×8 bindings + device settings.
- drag key → key to copy.
- drag profile → profile to copy.
- Import/Export Profile uses `*.profile.json`.
- Full preset schema is 4; schema 2/3 five-profile presets auto-migrate.

## Auto Sync

Firmware exposes a deterministic FNV-1a `KEYHASH` over all 25×8 bindings. On connect Studio compares it with the local preset:
- YES: PC → device
- NO: device → PC
- CANCEL: postpone

## Touch 2.4

Touch is no longer gated by resistive pressure. The raw axis wiring is taken from the known PIXEL-PRO hardware:

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
- each calibration target collects stable samples and uses median.

Four calibration points are TL → TR → BR → BL at (40,40), (439,40), (439,279), (40,279). Firmware solves a full affine transform and saves it to NVS key `touchcal2`.

**Reset Touch** removes the affine calibration and uses the known hardware fallback mapping.

**Touch Diagnostics** streams:
`E|TOUCHRAW|pressed|rawX|rawY|quality|mappedX|mappedY`.

## 25 profiles on LCD

The physical screen has five profile buttons at the bottom, so firmware displays the active bank of five. P1…P5, P6…P10, … P21…P25 are selected by the active bank; Studio/Auto Profile/native actions can jump to any profile.

## USB Safe Mode

PIXEL PRO does not expose USB mass storage. It remains HID + CDC, so the equivalent safe-mode behavior is always active by design.
