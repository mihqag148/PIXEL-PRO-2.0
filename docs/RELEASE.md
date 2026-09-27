## 2.2.0 — PC Monitoring Full Screen + eezbotfun-style feature parity

- Giữ toàn bộ v2.1.0: fix COM DTR+RTS, auto-probe COM, 5×8 keymap, HID/media, host macro, touch calibration, GIF screensaver, icon phím, RGB, orientation và ACK/CRC transfer.
- Thêm **PC Monitoring Full Screen** trên HX8357-B: CPU/GPU load, CPU/GPU temperature, RAM, disk và network.
- Studio dùng **LibreHardwareMonitor 0.9.6** cho CPU/GPU sensor, đồng thời có fallback Windows/.NET để không làm app lỗi khi sensor không được expose.
- Monitor cập nhật qua CDC mỗi giây; keyboard/media HID và macro host vẫn hoạt động trong lúc dashboard toàn màn hình đang hiển thị.
- Nút **PC Monitor** bật/tắt dashboard; mất COM tự dừng monitor phía Studio.
- System.IO.Ports được nâng lên 10.0.3 để đồng bộ dependency của LibreHardwareMonitor.
- Wireless/ESP-NOW của eezbotfun không được giả lập vì phần cứng mục tiêu là ESP32-S2 không có radio; các tính năng phần mềm phù hợp phần cứng được giữ trong PIXEL PRO 2.0.

### Flash

Flash Download Tool: dùng **PIXEL_PRO_2_merged.bin tại 0x000000** (ESP32-S2, DIO, 40 MHz, 4 MB). App-only **PIXEL_PRO_2_app.bin** tại **0x010000** chỉ khi partition v2 đã tồn tại.

Full merged flash có thể reset NVS/SPIFFS và xóa keymap đã lưu, touch calibration, GIF và icons. Preset JSON trên PC có thể xuất riêng trước khi nạp full.

### Provenance

Feature/workflow được đối chiếu với dự án MIT `eezbotfun/8-key-macropad`. PIXEL PRO 2.0 dùng protocol và implementation riêng cho ESP32-S2 + MCUFRIEND/HX8357-B. LibreHardwareMonitor được dùng theo MPL-2.0.

CI phải build/test firmware ESP32-S2 thật và Studio Windows x64 thành công trước khi release được tạo. Kiểm tra vật lý LCD/touch/USB vẫn cần xác nhận trên thiết bị thực tế.
