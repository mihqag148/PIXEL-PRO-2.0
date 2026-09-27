## 2.2.1 — USB handshake + HX8357-B orientation/startup fix

- Sửa lỗi **Windows có COM nhưng Studio timeout HELLO**:
  - Studio mở cổng với **DTR trước rồi RTS** để tránh đi nhầm chuỗi line-state reboot của Arduino-ESP32 cũ.
  - Firmware vẫn `enableReboot(false)` và không còn chặn reply bằng `USBCDC::operator bool()`; `USBCDC::write()`/TinyUSB tự xác định endpoint có host.
  - Giảm TX timeout của CDC để event không làm chậm loop nếu host ngắt bất ngờ.
- Giữ `CDCOnBoot=default` có chủ đích: PIXEL PRO 2.0 tự sở hữu CDC interface 0 qua `USBCDC usbLink`; bật core CDC song song có thể tạo interface cạnh tranh.
- Hiệu chỉnh gốc landscape của HX8357-B theo panel thực tế: MADCTL từ gốc `0x28` sang `0x68`. Bốn mode trên Studio vẫn giữ nghĩa: Hướng gốc / 180° / lật ngang / 180° + lật ngang.
- Migrate orientation sang key NVS `orient2` và mặc định về **Hướng gốc**, để giá trị sai đã lưu từ 2.2.0 không kéo màn quay/lật lại.
- Sửa nhiễu lúc khởi động: gửi **Display OFF ngay**, init controller, set orientation, clear toàn màn đen rồi mới **Display ON**.
- Giữ toàn bộ tính năng 2.2.0: 5×8 keymap, HID/media, host macro, touch calibration, GIF screensaver, icon phím, RGB và PC Monitoring fullscreen.
- ESP32-S2 có **2.4 GHz Wi-Fi và hỗ trợ ESP-NOW**, nhưng không có Bluetooth. Wireless/ESP-NOW chưa được triển khai trong 2.2.1; bản này vẫn dùng USB CDC/HID.

### Flash

Flash Download Tool: dùng **PIXEL_PRO_2_merged.bin tại 0x000000** (ESP32-S2, DIO, 40 MHz, 4 MB). App-only **PIXEL_PRO_2_app.bin** tại **0x010000** chỉ khi partition v2 đã tồn tại.

Nếu đang ở v2.0.0/v2.2.0 và COM/HID hoạt động không ổn định, ưu tiên nạp **merged** ở 0x000000 rồi rút/cắm USB lại. Sau khi boot, Studio 2.2.1 sẽ tự handshake với DTR→RTS.

### Validation

CI phải chạy model tests, source regression guards, firmware ESP32-S2 compile thật, app protocol tests, UI smoke test và Windows self-contained publish trước khi release được tạo. Kiểm tra vật lý LCD/touch/USB vẫn cần xác nhận trên thiết bị thật.
