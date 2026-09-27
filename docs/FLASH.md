# Nạp PIXEL PRO 2.0 v2.4

## Full flash

Flash Download Tool:
- chip: ESP32-S2
- file: `PIXEL_PRO_2_merged.bin`
- offset: **0x000000**
- mode: DIO
- frequency: 40 MHz
- flash: 4 MB

Chỉ tick merged. App-only `PIXEL_PRO_2_app.bin` dùng offset **0x010000** khi partition v2 đã tồn tại.

## ROM recovery

Nếu firmware không enumerate:
1. rút USB;
2. giữ BOOT;
3. cắm USB;
4. nhấn/thả RESET;
5. thả BOOT;
6. flash merged ở 0x000000.

## Touch sau khi nâng từ 2.3

Dùng **Studio 2.4**:
1. Connect / Auto Find.
2. Display & Media → **Reset Touch**.
3. bật **Touch Diagnostics**.
4. chạm TL/TR/BR/BL; raw X/Y phải thay đổi theo vị trí.
5. tắt diagnostics.
6. **Calibrate Touch**.
7. chạm và giữ nhẹ ~0,1 s tại từng dấu +, rồi nhả tay hoàn toàn trước điểm kế tiếp.
8. thứ tự TL → TR → BR → BL.

Studio sẽ hiện `CALPOINT` sau mỗi điểm. Nếu không capture được, giữ lâu hơn; firmware trả `CALWAIT`. Nếu geometry không hợp lệ, trả `CALFAIL|BAD_GEOMETRY`.

## esptool

```powershell
python -m pip install esptool==5.1.0
python -m esptool --chip esp32s2 --port COM5 write-flash 0x0 PIXEL_PRO_2_merged.bin
```

Full merged có thể reset NVS/SPIFFS. Export preset trước nếu cần. v2.4 không auto-format SPIFFS trong setup; filesystem chỉ được format on-demand khi upload media/icon/script.
