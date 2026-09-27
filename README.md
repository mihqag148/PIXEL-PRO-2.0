# PIXEL PRO 2.0

Firmware ESP32-S2 + Studio Windows cho bộ **LOLIN S2 Mini / MCUFRIEND HX8357-B 480×320**. Repo này là nhánh độc lập; mọi build/release của dự án này chỉ thuộc **PIXEL-PRO-2.0**.

## 2.2.1 — USB handshake + calibrated HX8357-B startup

- Sửa handshake COM native USB CDC: Studio assert **DTR trước rồi RTS**, firmware tắt reboot theo line-state và không còn chặn reply bằng wrapper `USBCDC::operator bool()`.
- Hiệu chỉnh gốc landscape cho panel thực tế sang MADCTL `0x68`; orientation cũ được migrate sang key mới và mặc định trở về **Hướng gốc**.
- LCD được `Display OFF` ngay khi init, set orientation + clear đen trước `Display ON`, giảm nhiễu/GRAM rác lúc bật máy.
- 8 phím × 5 profile, HID keyboard/media chạy độc lập không cần Studio; host action chạy khi Studio ở nền.
- Macro host: text Unicode, shortcut, mở app/file/URL, delay, mouse move/click/wheel, KeyDown/KeyUp.
- Touch calibration 4 điểm lưu NVS; hướng/lật màn hình và touch dùng chung transform.
- GIF screensaver: Studio chuyển GIF thành RGB332 160×106, giữ thời lượng hợp lý, truyền raw 512-byte có ACK/CRC, lưu SPIFFS và phát trực tiếp lên HX8357-B.
- Icon tùy chỉnh từng phím: Studio chuyển ảnh về 48×48 RGB332, truyền có ACK/CRC, lưu flash và hiển thị trực tiếp trong tile.
- RGB per-key, brightness, profile strip, SD detect, import/export preset, tray mode.
- PC Monitoring Full Screen: Studio đọc CPU/GPU load + nhiệt độ qua LibreHardwareMonitor, RAM/disk/network qua Windows/.NET và cập nhật LCD mỗi giây; HID/phím vẫn hoạt động khi monitor đang hiển thị.
- Pinout HX8357-B/i8080 và toàn bộ phần cứng hiện có được giữ nguyên.

Kiến trúc và cách vận hành tham khảo dự án MIT [eezbotfun/8-key-macropad](https://github.com/eezbotfun/8-key-macropad), nhưng protocol và implementation của PIXEL PRO 2.0 được viết riêng cho ESP32-S2 + HX8357-B; không yêu cầu firmware nhị phân hay assets của eezbotfun.

## Tải và sử dụng

Tải firmware và Studio tại [GitHub Releases](https://github.com/mihqag148/PIXEL-PRO-2.0/releases).
Xem [hướng dẫn nạp](docs/FLASH.md), [pinout](docs/HARDWARE.md), [protocol](docs/PROTOCOL.md) và [Studio](docs/STUDIO.md).

### Flash Download Tool

- Full image: **PIXEL_PRO_2_merged.bin** tại **0x000000**.
- App-only: **PIXEL_PRO_2_app.bin** tại **0x010000**, chỉ khi partition v2 đã tồn tại.
- ESP32-S2, DIO, 40 MHz, 4 MB.
- Full image có thể reset NVS; preset PC nên được xuất trước nếu cần giữ cấu hình.

## Hardware

- LCD MCUFRIEND/HX8357-B 480×320, bus i8080 8-bit.
- 8 phím ma trận 2×4.
- WS2812 per-key RGB.
- Encoder/roller volume + mute.
- Touch resistive dùng chung một số line với shield.
- microSD tùy chọn.
- D17/D18 giữ dành cho bus mở rộng.

Không thay đổi dây so với pinout hiện tại của PIXEL PRO 2.0.

## Build

Firmware: Arduino CLI 1.5.1, Arduino-ESP32 3.3.12, Adafruit GFX 1.11.11, Adafruit NeoPixel 1.15.5.

```sh
arduino-cli compile --fqbn 'esp32:esp32:esp32s2:PSRAM=enabled,CDCOnBoot=default,MSCOnBoot=default,DFUOnBoot=default,FlashMode=dio' --output-dir build/raw firmware/PixelPro2
dotnet run --project tests/AppTests/AppTests.csproj -c Release
dotnet publish app/PixelPro2/PixelPro2.csproj -c Release -r win-x64 --self-contained true -o dist/studio
g++ -std=c++17 -Wall -Wextra -Werror tests/model_test.cpp -o build/model-test
```

LibreHardwareMonitor 0.9.6 được dùng cho telemetry CPU/GPU trên Windows; nếu sensor không khả dụng app tự fallback cho CPU/RAM/disk/network.

CI chỉ tạo tag/release mới khi firmware thật và Studio đều build/test thành công. CI xanh xác nhận build phần mềm, không thay thế kiểm tra trực tiếp timing LCD/touch/USB trên thiết bị vật lý.
