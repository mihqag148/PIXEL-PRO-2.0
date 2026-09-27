# Nạp PIXEL PRO 2.0

## Flash Download Tool (Windows)

Chọn **ESP32-S2** / **SPI Download**.

Full install / recovery:

| File | Offset |
|---|---:|
| `PIXEL_PRO_2_merged.bin` | **0x000000** |

Chỉ tick **một** dòng merged. Chọn:

- Flash Mode: **DIO**
- Frequency: **40 MHz**
- Flash: **4 MB**

Nhấn START. Khi FINISH, nhấn RESET hoặc rút/cắm lại USB.

> Không flash `PIXEL_PRO_2_merged.bin` tại 0x10000.

App-only update, chỉ khi thiết bị đã có partition v2 đúng:

| File | Offset |
|---|---:|
| `PIXEL_PRO_2_app.bin` | **0x010000** |

Không tick merged và app-only cùng lúc.

## Recovery khi firmware không lên COM/HID

ESP32-S2 có ROM USB download mode độc lập firmware:

1. Rút USB.
2. Giữ **BOOT**.
3. Cắm USB.
4. Nhấn/thả **RESET**.
5. Thả **BOOT**.
6. Chọn COM ROM mới xuất hiện và flash merged ở 0x000000.

## esptool

```powershell
python -m pip install esptool==5.1.0
python -m esptool --chip esp32s2 --port COM5 write-flash 0x0 PIXEL_PRO_2_merged.bin
```

Hoặc giải nén `PIXEL-PRO-2.0-firmware.zip` và chạy:

```powershell
python flash_firmware.py --port COM5
```

Script kiểm tra SHA256 và dùng đúng merged offset.

## Sau khi flash v2.3.0

- USB phải enumerate HID + CDC.
- Studio 2.3 mở CDC bằng DTR → RTS.
- LCD được giữ tắt trong lúc controller init, dùng timing ổn định của v2.2.1, clear đen trước Display ON rồi render UI.
- Full flash không còn format SPIFFS trong `setup()`; vì vậy boot đầu tiên không nên bị đứng lâu để tạo filesystem.
- SPIFFS chỉ được format on-demand khi lần đầu upload GIF/icon/PXS2 script nếu partition đang trống.
- SD chỉ được probe khi Studio hỏi.
- Touch nên được **Calibrate Touch** lại sau khi nâng từ bản cũ để tận dụng mapping 24…455 / 24…295 mới.

## Dữ liệu

Full merged có thể reset NVS/SPIFFS tùy cách flash/erase. Export preset JSON trước nếu cần giữ cấu hình.

Partition v2:

- factory app: 0x10000 / 0x200000
- SPIFFS: 0x210000 / 0x1F0000

## Kiểm tra file

Release kèm `SHA256SUMS.txt`. Windows có thể hiện SmartScreen vì Studio chưa ký code-signing certificate. Studio là self-contained .NET 8 x64.

Nếu LCD không lên nhưng USB vẫn có HID/COM, kiểm tra nguồn/backlight/controller/pinout trước khi thay USB firmware. Nếu USB ROM BOOT cũng không xuất hiện, kiểm tra cáp data và đường native USB D19/D20.
