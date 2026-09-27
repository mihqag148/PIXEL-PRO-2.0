## 2.5.0 — Plugin architecture + Studio parity

### Studio workflow

- 25 profiles × 8 keys.
- multi-device COM sessions with active-device switching.
- drag/drop actions and key/profile copy.
- 512 actions/key.
- HID Mode / App Mode per key.
- SAVE KEY / SAVE PROFILE / SAVE TO DEVICE.
- full preset + single-profile import/export.
- Auto Sync PC ↔ device via KEYHASH.
- Auto Profile by foreground Windows process.
- App Mode keeps running in the system tray.
- Light/Dark plus English / Vietnamese / Simplified Chinese.
- Device List context menu.
- permanent Safe Mode indicator for HID+CDC-only USB behavior.

### Scripts and HID

- native PXS2 scripts retained.
- eez-style Script action supports common DELAY / STRING / REPEAT / mouse / media / profile / default timing commands.
- F1…F24, Print Screen, navigation and numpad HID keys.
- Mouse Move native script opcode added.
- timed text/chords supported in native scripts.

### Preset Gallery and key icons

- built-in preset gallery.
- search/category/filter-duplicates UI.
- local preset folder in AppData.
- downloaded preset import.
- exports default to AppData.
- key icons can be chosen offline and previewed on K1…K8.
- normal Save Key/Profile/Device workflow synchronizes pending icons.
- Media Control automatically generates matching key icons.

### Named Pipe plugins

Studio no longer loads LibreHardwareMonitor directly.

- `PIXEL_PRO_2_PLUGINS` named pipe service.
- standalone `PixelPro2.PcMonitorPlugin.exe`.
- standalone `PixelPro2.MusicPlugin.exe`.
- PC Monitor plugin sends CPU/GPU/RAM/Disk/Network/temp telemetry.
- Music plugin uses Windows SMTC and sends Now Playing data.
- Studio forwards plugin data to the active PIXEL PRO.
- plugin status is visible in the UI and bottom bar.
- main Studio stays `asInvoker` and does not require admin privileges.

### Touch / firmware

Retains v2.4 affine touch rebuild:
- verified MCUFRIEND raw-axis geometry.
- median filtering and four-point affine calibration.
- Reset Touch + Touch Diagnostics.
- 25-profile firmware model.
- MUSIC and MONITOR full-screen modes.
- stable HX8357-B timing.
- lazy SPIFFS/SD startup.

### Flash

Use **PIXEL_PRO_2_merged.bin** at **0x000000** with ESP32-S2 / DIO / 40 MHz / 4 MB.

After flashing:
1. Open matching Studio 2.5.
2. Connect PIXEL PRO.
3. Display & Media → Reset Touch.
4. Touch Diagnostics ON and verify all corners.
5. Touch Diagnostics OFF.
6. Calibrate Touch: TL → TR → BR → BL.

CI requires firmware model/source tests, actual ESP32-S2 compile, merged image verification, app protocol tests, real Studio UI smoke, Studio publish, both standalone plugin publishes and release asset verification.
