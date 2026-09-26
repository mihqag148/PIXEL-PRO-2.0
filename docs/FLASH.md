# Nạp PIXEL PRO 2.0

1. Tải `PIXEL-PRO-2.0-firmware.zip`, giải nén và đọc `HARDWARE.md`.
2. Đóng Studio hoặc ứng dụng đang giữ cổng COM.
3. Giữ BOOT, nhấn/thả RESET, thả BOOT để vào ROM download của ESP32-S2. Dùng cổng USB native, cáp có data.
4. Cài Python và `python -m pip install esptool==5.1.0`.
5. Thay COM5 bằng cổng thực tế rồi nạp:

```powershell
python -m esptool --chip esp32s2 --port COM5 write-flash 0x0 PIXEL_PRO_2_merged.bin
```

6. Nhấn RESET hoặc rút/cắm USB. Thiết bị có HID keyboard/consumer và cổng CDC. Chờ màn hình khởi động, thử A…H trong trình soạn thảo.
7. Giải nén `PIXEL-PRO-2.0-Studio-win-x64.zip`, chạy `PixelPro2.exe`, chọn cổng và Kết nối.

`PIXEL_PRO_2_merged.bin` là ảnh đã merge với offset đầu **0x0**; không nạp ở 0x1000. Các ảnh rời: bootloader 0x1000, partitions 0x8000, boot_app0 0xe000, app 0x10000. `PIXEL_PRO_2_app.bin` chỉ dành cập nhật thiết bị đã có partition 2.0.0.

Partition mới: factory 0x10000 / 0x200000; vùng filesystem dự phòng 0x210000 / 0x1F0000. Không dùng app-only với partition cũ. Cấu hình v2 sử dụng namespace NVS riêng. Sao lưu preset cũ bằng app cũ trước khi đổi firmware; preset v1 không tự nhập sang v2.

Kiểm tra SHA256 trong `SHA256SUMS.txt` ở Release. Windows có thể hiện SmartScreen vì app chưa ký số. App tự chứa .NET 8, không cần cài .NET riêng.

Nếu mất CDC/HID, vào ROM download bằng BOOT/RESET và nạp lại merged. Nếu màn tối kiểm tra lại 3V3 POWER + RD, nguồn 5V và đúng controller HX8357-B trước khi đổi chân hoặc firmware.
