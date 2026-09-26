# Nạp PIXEL PRO 2.0

## Flash Download Tool (Windows)

Chọn **ESP32-S2**, chế độ **SPI Download**. Đối với bản đầy đủ:

| Ô file được chọn | Địa chỉ nhập bên phải |
|---|---|
| PIXEL_PRO_2_merged.bin | **0x000000** |

Chỉ tick dòng này; bỏ tick các file khác. Chọn **DIO**, **40 MHz**, **4 MB**, cổng COM ROM download đúng của ESP32-S2 rồi START. Khi FINISH, nhấn RESET hoặc rút/cắm lại USB để thoát bootloader. Không cần thay đổi dây màn hình.

Nếu chỉ cập nhật app trên thiết bị đã có partition v2, chọn duy nhất `PIXEL_PRO_2_app.bin` tại **0x010000**. Không dùng cùng offset cho hai loại file và không tick cả merged lẫn app. Merged nạp ở địa chỉ của app có thể không khởi động, để lại LCD trắng; không thể kết luận đây là nguyên nhân nếu chưa kiểm tra offset thực tế.

## esptool (cách khác)

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

Hoặc sau khi cài esptool, chạy `python flash_firmware.py --port COM5` trong thư mục giải nén. Script kiểm tra SHA256 và tự chọn merged/offset 0x0.

Bản 2.0.1 mặc định xoay 180° so với 2.0.0. Nếu vị trí lắp màn khác, dùng Studio 2.0.1 chọn hướng màn hình và **Áp dụng hướng**. Cảm ứng đổi hướng đồng bộ. Studio 2.0.0 không nhận handshake 2.0.1 nên cần tải app mới cùng bản.

`PIXEL_PRO_2_merged.bin` là ảnh đã merge với offset đầu **0x0**; không nạp ở 0x1000. Các ảnh rời: bootloader 0x1000, partitions 0x8000, boot_app0 0xe000, app 0x10000. `PIXEL_PRO_2_app.bin` chỉ dành cập nhật thiết bị đã có partition 2.0.0.

Partition mới: factory 0x10000 / 0x200000; vùng filesystem dự phòng 0x210000 / 0x1F0000. Không dùng app-only với partition cũ. Cấu hình v2 sử dụng namespace NVS riêng. Sao lưu preset cũ bằng app cũ trước khi đổi firmware; preset v1 không tự nhập sang v2.

Kiểm tra SHA256 trong `SHA256SUMS.txt` ở Release. Windows có thể hiện SmartScreen vì app chưa ký số. App tự chứa .NET 8, không cần cài .NET riêng.

Nếu mất CDC/HID, vào ROM download bằng BOOT/RESET và nạp lại merged. Nếu màn tối kiểm tra lại 3V3 POWER + RD, nguồn 5V và đúng controller HX8357-B trước khi đổi chân hoặc firmware.
