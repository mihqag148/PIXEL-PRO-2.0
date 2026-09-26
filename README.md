# PIXEL PRO 2.0

Firmware ESP32-S2 + app Windows mới, được phát triển độc lập cho bộ **LOLIN S2 Mini / MCUFRIEND HX8357-B 480×320** của người dùng.

**2.0.1 — sửa hướng hiển thị và ổn định LCD:** mặc định xoay 180° so với 2.0.0; Studio có tùy chọn hướng/lật ngang. Phục hồi init tối giản của HX8357-B, tăng khoảng ổn định bus, giữ nguyên pinout và cấu trúc keymap. Với Flash Download Tool: merged tại **0x0**, app-only tại **0x10000**; merged có thể reset NVS, xem hướng dẫn bên dưới. Các thay đổi phần cứng vẫn cần người dùng xác nhận sau nạp.

## Tải và sử dụng

Tải firmware và Studio tại [GitHub Releases](https://github.com/mihqag148/PIXEL-PRO-2.0/releases).
Xem [hướng dẫn nạp](docs/FLASH.md), [pinout](docs/HARDWARE.md) và [hướng dẫn app](docs/STUDIO.md).

- 8 phím ma trận 2×4, 5 profile lưu trên thiết bị; nhãn ASCII và màu riêng từng phím.
- USB HID keyboard/consumer hoạt động khi không mở app; roller tăng/giảm âm lượng và nhấn mute.
- App Windows x64 quản lý profile, RGB, nhập/xuất preset JSON và chạy chuỗi macro (text Unicode, shortcut, mở ứng dụng/file/URL, delay).
- LCD HX8357-B i8080 8-bit; cảm ứng vùng chân màn hình chuyển profile.
- Native USB CDC có request ID, giới hạn kích thước và ACK. Không chờ app khi khởi động.
- Thẻ microSD tùy chọn được nhận diện; D17/D18 giữ dành riêng cho bus mở rộng.

Kiến trúc HID độc lập + app thực thi tác vụ PC tham khảo cách vận hành công khai của [eezbotfun](https://github.com/eezbotfun/8-key-macropad). Không sử dụng firmware nhị phân, mã nguồn hoặc tài sản độc quyền của họ. Không phải firmware tương thích giao thức eezbotfun/LumiPad.

## Phạm vi bản 2.0.0

Đây là bản nền tảng mới, không phải bản sao toàn bộ tính năng PIXEL-PRO cũ. Chưa có GIF/background, ảnh icon tùy chỉnh, USB MSC, cập nhật OTA, tự đổi profile theo ứng dụng, hoặc điều khiển module PCA9546A. ESP32-S2 không có Bluetooth. Macro PC cần mở Studio (có thể thu vào khay) và bật cho phép macro. Nhãn LCD dùng ASCII tối đa 12 ký tự; macro text hỗ trợ Unicode.

Pinout và các thanh ghi panel lấy từ tài liệu/lịch sử phần cứng repo cũ ở commit `8086533652f131a41edf0c38a80da5b109643350`, chỉ đọc để tham khảo. Mọi thay đổi và release thuộc duy nhất repo này.

CI build firmware thật, publish app Windows tự chứa .NET, chạy kiểm thử và kiểm tra partition/asset trước khi phát hành. CI xanh không thay thế kiểm tra trực tiếp màn hình, cảm ứng, timing bus và USB trên thiết bị người dùng. Xem [kiểm thử](docs/VALIDATION.md).

## Build

Firmware: Arduino CLI 1.5.1, Arduino-ESP32 3.3.12, Adafruit GFX 1.11.11, Adafruit NeoPixel 1.15.5. Xem workflow để cài thư viện và chọn FQBN chính xác.

```sh
arduino-cli compile --fqbn 'esp32:esp32:esp32s2:PSRAM=enabled,CDCOnBoot=default,MSCOnBoot=default,DFUOnBoot=default,FlashMode=dio' --output-dir build/raw firmware/PixelPro2
dotnet run --project tests/AppTests/AppTests.csproj -c Release
dotnet publish app/PixelPro2/PixelPro2.csproj -c Release -r win-x64 --self-contained true -o dist/studio
g++ -std=c++17 -Wall -Wextra -Werror tests/model_test.cpp -o build/model-test
```

Phát hành từ `main` chỉ sau khi cả job firmware và app thành công. Tag `v<VERSION>` không bị ghi đè.
