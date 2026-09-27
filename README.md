# PIXEL PRO 2.0

Firmware ESP32-S2 + Studio Windows cho **LOLIN S2 Mini / MCUFRIEND HX8357-B 480×320**. Repo này độc lập; build/release chỉ thuộc **PIXEL-PRO-2.0**.

## 2.4.0 — touch affine + 25 profiles + Studio parity

### Touch được viết lại

v2.3 đã giả định sai hướng raw của panel thật. Phần cứng này dùng:

- XP = D39
- XM = D14
- YP = D13
- YM = D40
- rawX được lấy từ YP/D13
- rawY được lấy từ XM/D14
- screen X theo **rawY đảo chiều**
- screen Y theo **rawX**

Mapping fallback đã xác nhận từ PIXEL-PRO cũ:

```text
screenX = map(rawY, 942, 139, 0, 479)
screenY = map(rawX, 136, 907, 0, 319)
```

Calibration mới không còn dùng pressure làm điều kiện sống/chết. Mỗi lần chạm phải ổn định qua 3 poll liên tiếp; release cần 4 miss. Calibration thu nhiều mẫu/điểm, lấy median và giải **affine 4 điểm**, nên tự xử lý swap trục, đảo chiều và skew nhẹ của tấm resistive.

Studio có **Reset Touch** và **Touch Diagnostics** để xem trực tiếp rawX/rawY/quality/mapped X,Y.

### 25 profiles

- 25 profile × 8 key.
- Config cũ 5-profile được migrate vào P1…P5; P6…P25 dùng default.
- LCD hiển thị bank 5 profile tương ứng P1…P5, P6…P10, … P21…P25.
- Auto Profile hỗ trợ P1…P25.
- Change Profile / Profile Next / Profile Previous hỗ trợ đủ 25 profile.
- Preset JSON schema 4; preset schema 2/3 cũ được migrate.

### Studio 2.4

Workflow được tổ chức sát configurator eezbotfun hơn:

- Profile & Key Selection bên trái, Device List phía dưới.
- Configuration bên phải với Action toolbox → Action Sequence → Parameters.
- 25 profile selector.
- drag/drop action.
- drag key → key và profile → profile để copy.
- **SAVE KEY / SAVE PROFILE / SAVE TO DEVICE**.
- **Import/Export Profile** riêng.
- HID Mode theo từng key.
- tối đa **512 actions/key**.
- USB Keyboard / Consumer / Mouse HID + PXS2 native script.
- Auto Profile theo app foreground.
- **Auto Sync**: so sánh KEYHASH giữa PC/device và hỏi sync theo chiều nào khi khác nhau.
- Getting Started + HID Mode explanation.
- Safe Mode theo thiết kế: không expose USB mass-storage.
- Power Off Computer.
- PC Monitor fullscreen.
- key icon, RGB, GIF screensaver, auto screen-off.
- Light/Dark.

### Boot / LCD

- giữ timing HX8357-B ổn định đã xác nhận từ v2.2.1;
- không auto-format SPIFFS trong setup;
- SD probe lazy;
- native USB CDC/HID không chờ Studio.

## Flash

Full install/recovery:

- **PIXEL_PRO_2_merged.bin**
- offset **0x000000**
- ESP32-S2
- DIO
- 40 MHz
- 4 MB

App-only **PIXEL_PRO_2_app.bin** ở **0x010000** chỉ khi partition v2 đã tồn tại.

Sau khi nâng từ v2.3, nên chạy **Reset Touch → Calibrate Touch** lại.

## Ghi chú parity

2.4.0 đưa các workflow chính công khai của eezbotfun vào implementation riêng cho PIXEL PRO 2.0: 25 profiles, per-key HID Mode, 512 actions/key, Save Key/Profile, profile import, drag/copy, Auto Sync, Getting Started, Power Off, PC Monitor và auto screen-off.

Không tuyên bố byte-for-byte clone. SMTC Now Playing/album art, community preset online, simultaneous multi-device sessions, custom `cus` protocol và ESP-NOW chưa nằm trong release này.

## Build

Pinned: Arduino-ESP32 3.3.12, Adafruit GFX 1.11.11, Adafruit NeoPixel 1.15.5, .NET 8 win-x64, System.IO.Ports 10.0.3, LibreHardwareMonitor 0.9.6.
