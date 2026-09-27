# PIXEL PRO 2.0

Firmware ESP32-S2 + Studio Windows cho **LOLIN S2 Mini / MCUFRIEND HX8357-B 480×320**. Repo này độc lập; build/release chỉ thuộc **PIXEL-PRO-2.0**.

## 2.5.0 — Studio parity + plugin architecture

### Studio 2.5

Workflow được tổ chức gần configurator eezbotfun hơn nhưng vẫn là implementation riêng cho PIXEL PRO 2.0:

- 25 profiles × 8 keys.
- Profile & Key Selection bên trái, Device List phía dưới.
- Configuration bên phải với Action toolbox → Action Sequence → Parameters.
- drag/drop action.
- drag key → key và profile → profile để copy.
- **SAVE KEY / SAVE PROFILE / SAVE TO DEVICE**.
- Import/Export full preset và từng Profile.
- HID Mode / App Mode theo từng key.
- tối đa **512 actions/key**.
- Script action tương thích cú pháp eez phổ biến, gồm delay/text/repeat/mouse/profile/F1…F24/numpad.
- Auto Profile theo app foreground.
- **Auto Sync** PC ↔ device qua KEYHASH.
- multi-device COM sessions: có thể giữ nhiều PIXEL PRO kết nối và chuyển active device.
- App Mode tiếp tục chạy khi minimize/close xuống system tray.
- Light/Dark + English / Tiếng Việt / 简体中文.
- context menu Device List.
- Safe Mode hiển thị rõ: USB mass-storage luôn ẩn trên PIXEL PRO 2.0.

### Plugin architecture

Studio chính **không còn load LibreHardwareMonitor trực tiếp**.

- Named pipe service: `PIXEL_PRO_2_PLUGINS`.
- `PixelPro2.PcMonitorPlugin.exe`: đọc CPU/GPU/RAM/Disk/Network/temperature rồi gửi telemetry qua pipe.
- `PixelPro2.MusicPlugin.exe`: đọc Windows SMTC và gửi Now Playing qua pipe.
- Studio chỉ nhận dữ liệu plugin rồi forward sang thiết bị.
- plugin có thể bật/tắt trong tab **Plugins**.
- trạng thái plugin hiển thị ở bottom bar.
- kiến trúc pipe cho phép mở rộng plugin bên thứ ba sau này.
- configurator chính vẫn chạy `asInvoker`, không yêu cầu admin.

### Preset / icon workflow

- built-in presets cho Windows, Media, Browser, Office, OBS Studio, Fusion 360, DaVinci Resolve, Photoshop.
- Presets Gallery có search, category, **Filter Duplicated**, local presets và import downloaded preset.
- preset folder: `%LOCALAPPDATA%\PixelPro2\presets`.
- export folder mặc định: `%LOCALAPPDATA%\PixelPro2\exports`.
- icon được chọn **offline**, preview ngay trên K1…K8 rồi sync khi Save.
- Media Control tự tạo icon Play/Next/Prev/Stop/Volume/Mute tương ứng.

### Touch affine

Touch dùng mapping phần cứng MCUFRIEND đã xác nhận:

- XP = D39
- XM = D14
- YP = D13
- YM = D40
- rawX lấy từ YP/D13
- rawY lấy từ XM/D14
- screen X theo **rawY đảo chiều**
- screen Y theo **rawX**

Fallback:

```text
screenX = map(rawY, 942, 139, 0, 479)
screenY = map(rawX, 136, 907, 0, 319)
```

Calibration thu nhiều mẫu, median + **affine 4 điểm**, xử lý swap trục, đảo chiều và skew nhẹ. Studio có Reset Touch và Touch Diagnostics.

### Boot / LCD / USB

- giữ timing HX8357-B ổn định đã xác nhận từ v2.2.1.
- không auto-format SPIFFS trong setup.
- SD probe lazy.
- native USB CDC/HID không chờ Studio.
- USB Keyboard / Consumer / Mouse HID.
- PXS2 native scripts trong SPIFFS.
- GIF/screensaver, per-key icon, RGB, PC Monitor, SMTC music display, auto screen-off.

## Flash

Full install/recovery:

- **PIXEL_PRO_2_merged.bin**
- offset **0x000000**
- ESP32-S2
- DIO
- 40 MHz
- 4 MB

App-only **PIXEL_PRO_2_app.bin** ở **0x010000** chỉ khi partition v2 đã tồn tại.

Sau full flash, nên chạy **Reset Touch → Calibrate Touch** lại.

## Ghi chú parity

2.5.0 đưa phần lớn workflow desktop công khai của eezbotfun vào implementation riêng: 25 profiles, HID/App Mode, 512 actions/key, Save Key/Profile, profile import/export, drag/copy, Auto Sync, Getting Started, Power Off, F13…F24, media auto-icon, Preset Gallery, language switch, multi-device sessions, tray background, integrated plugin controls, SMTC music và Named Pipe plugin service.

Không tuyên bố byte-for-byte clone. Custom `cus` display protocol, ESP-NOW/wireless và preset cloud format của eezbotfun không được sao chép vào PIXEL PRO 2.0.

## Build

Pinned: Arduino-ESP32 3.3.12, Adafruit GFX 1.11.11, Adafruit NeoPixel 1.15.5, .NET 8 win-x64, System.IO.Ports 10.0.3. LibreHardwareMonitor 0.9.6 chỉ nằm trong **PC Monitor plugin** riêng.
