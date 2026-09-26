Firmware ESP32-S2 + Studio Windows x64 được build và kiểm thử từ cùng commit.

- 5 profile / 8 phím, HID độc lập, USB CDC, LCD HX8357-B, roller và RGB.
- Studio soạn preset, chạy macro PC, import/export JSON và thu vào khay.
- Pinout giữ theo bộ LOLIN S2 Mini / MCUFRIEND của người dùng; xem HARDWARE.md trong gói firmware.

Tải **PIXEL-PRO-2.0-firmware.zip** để có ảnh nạp và hướng dẫn. Nạp **PIXEL_PRO_2_merged.bin tại 0x0**. App-only chỉ cho partition v2. Tải và giải nén **PIXEL-PRO-2.0-Studio-win-x64.zip**, chạy PixelPro2.exe.

Build và kiểm thử phần mềm đã qua; chưa xác nhận trên thiết bị vật lý của người dùng. Bản 2.0.0 chưa có GIF, icon ảnh tùy chỉnh, MSC, OTA hoặc điều khiển module mở rộng. Không tương thích giao thức/preset LumiPad cũ. Macro cần app mở và bật cho phép macro.
