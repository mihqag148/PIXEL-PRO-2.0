# PIXEL PRO 2.0

Firmware ESP32-S2 + Studio Windows cho bộ **LOLIN S2 Mini / MCUFRIEND HX8357-B 480×320**. Repo này độc lập; mọi build/release ở đây chỉ thuộc **PIXEL-PRO-2.0**.

## 2.3.0 — touch / fast boot / Studio workflow overhaul

### Thiết bị

- 8 phím × 5 profile.
- USB HID keyboard + media + **mouse HID**.
- **HID Mode theo từng key**: chuỗi action có thể chạy ngay trên ESP32-S2, không cần Studio.
- Native **PXS2 script** tối đa **512 actions/key**, 8192 byte/key.
- App Mode cho action cần Windows: mở website/app/file/folder, mouse move và các host action khác.
- RGB từng key, roller volume/mute, icon 48×48, GIF screensaver, PC Monitor fullscreen.
- Auto screen-off: Always On / 30 s / 5 min / 15 min; HID vẫn chạy, phím/touch/roller đánh thức màn.
- LCD HX8357-B dùng gốc landscape MADCTL `0x68`.

### Touch v2.3

- median-of-5 ADC sampling,
- scan mỗi 8 ms,
- threshold nhạy hơn,
- sửa lỗi toán calibration: 4 điểm calibration nằm ở **24…455 / 24…295**, nên raw endpoints giờ được map về đúng các tọa độ target này thay vì sai thành mép 0…479 / 0…319,
- event debug có x/y/pressure.

### Boot sau full flash

- không còn `SPIFFS.begin(true)` trong setup,
- không format filesystem trước khi UI lên,
- SPIFFS mount không-format; format chỉ khi lần upload đầu thực sự cần,
- SD chỉ probe khi Studio hỏi,
- giữ lại timing HX8357-B đã ổn định của v2.2.1 để tránh trạng thái backlight sáng xám/GRAM chưa sẵn sàng; phần boot nhanh hơn đến từ việc bỏ format SPIFFS lúc `setup()` và trì hoãn probe SD.

### Studio 2.3

Studio được bố trí lại theo workflow kiểu eezbotfun/Stream Deck:

- bên trái: **Profile & Key Selection + Device List**,
- bên phải: **Action toolbox → Action Sequence → Parameters**,
- kéo/thả action vào sequence,
- kéo key sang key để copy,
- kéo profile sang profile để copy,
- **SAVE KEY / SAVE PROFILE / SAVE TO DEVICE**,
- HID Mode / App Mode theo key,
- tối đa 512 action/key,
- Dynamic / Auto Profile theo app Windows đang foreground,
- Import/Export preset schema 3,
- Light/Dark mode,
- tab Display & Media + Auto Profile + Log.

Action toolbox: Website, Launch App, Open Folder, Open File, Text, Shortcut, Wait, Mouse Move, Mouse Click, Wheel, Media, Change Profile, Functional Key, Device Control.

## Mức tương thích eezbotfun

Kiến trúc UX và feature organization được đối chiếu với dự án MIT [eezbotfun/8-key-macropad](https://github.com/eezbotfun/8-key-macropad), nhưng PIXEL PRO 2.0 dùng protocol/firmware riêng cho ESP32-S2 + HX8357-B.

2.3.0 đã đưa các phần chính về cùng kiểu workflow: 5×8 key, device list, drag/drop sequence, per-key HID Mode, app actions, mouse/media HID, scripts, 512 actions/key, Save Key/Profile, dynamic profile, icon/RGB, PC monitor và auto screen-off.

**Chưa tuyên bố clone 1:1 toàn bộ**: simultaneous multi-device sessions, community preset gallery, SMTC Now Playing/album art, custom `cus` display protocol và ESP-NOW wireless chưa có trong 2.3.0.

## Tải / Flash

Tải firmware và Studio tại [GitHub Releases](https://github.com/mihqag148/PIXEL-PRO-2.0/releases).

Full flash:

- `PIXEL_PRO_2_merged.bin`
- offset **0x000000**
- ESP32-S2
- DIO
- 40 MHz
- 4 MB

App-only `PIXEL_PRO_2_app.bin` tại **0x010000** chỉ dùng khi partition v2 đã tồn tại.

Xem thêm:

- [FLASH.md](docs/FLASH.md)
- [HARDWARE.md](docs/HARDWARE.md)
- [STUDIO.md](docs/STUDIO.md)
- [PROTOCOL.md](docs/PROTOCOL.md)
- [VALIDATION.md](docs/VALIDATION.md)

## Hardware

- HX8357-B / MCUFRIEND 480×320, i8080 8-bit.
- 8-key 2×4 matrix.
- 8× WS2812/SK6812-style per-key RGB chain.
- encoder/roller + push.
- resistive touch sharing shield lines.
- optional microSD.
- D17/D18 reserved for future expansion.
- native USB D19/D20 reserved.

Không thay đổi pinout của PIXEL PRO 2.0 trong release này.

## Build

Pinned build:

- Arduino CLI 1.5.1
- Arduino-ESP32 3.3.12
- Adafruit GFX 1.11.11
- Adafruit NeoPixel 1.15.5
- .NET 8 Windows x64
- System.IO.Ports 10.0.3
- LibreHardwareMonitor 0.9.6

```sh
arduino-cli compile --fqbn 'esp32:esp32:esp32s2:PSRAM=enabled,CDCOnBoot=default,MSCOnBoot=default,DFUOnBoot=default,FlashMode=dio' --output-dir build/raw firmware/PixelPro2
dotnet run --project tests/AppTests/AppTests.csproj -c Release
dotnet run --project tests/UiSmoke/UiSmoke.csproj -c Release
dotnet publish app/PixelPro2/PixelPro2.csproj -c Release -r win-x64 --self-contained true -o dist/studio
```

CI xanh xác nhận build/tests phần mềm. Touch sensitivity, LCD timing và USB vẫn cần xác nhận cuối cùng trên phần cứng thật.
