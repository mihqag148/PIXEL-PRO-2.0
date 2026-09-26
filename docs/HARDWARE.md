# Pinout giữ nguyên theo bộ phần cứng người dùng

Board: LOLIN/WEMOS S2 Mini, ESP32-S2, flash 4 MB, PSRAM 2 MB.
Tên Dxx trên silkscreen được ánh xạ thành GPIO xx trong `Pins.h`.

| Nhóm | Đấu dây |
|---|---|
| Matrix ROW1, ROW2 | D1, D2 |
| Matrix COL1…COL4 | D3, D4, D5, D6 |
| Roller A, B, push | D7, D8, D21; COM và chân push còn lại xuống GND |
| microSD SCK, DO/MISO, DI/MOSI, CS | D9, D10, D11, D12 |
| LCD WR, RS/DC, CS | D13, D14, D16 |
| LCD D0…D7 | D33, D34, D35, D36, D37, D38, D39, D40 |
| LCD RST | EN, không phải D17 |
| LCD RD | 3V3, không phải D12 |
| Shield 3V3 POWER | 3V3; bắt buộc trên shield đã đo của người dùng |
| Shield 5V, GND | 5V/VBUS, GND |
| RGB DATA | D15; LED1…8 tương ứng K1,K2,K3,K4,K8,K7,K6,K5 |
| Bus mở rộng SCL/SDA | D17/D18 được giữ riêng, firmware 2.0.0 chưa điều khiển module |
| Native USB D−/D+ | D19/D20, không dùng cho GPIO khác |

Matrix: hàng 1 = K1…K4; hàng 2 = K5…K8. Mỗi phím có diode theo chiều `COL -> switch -> diode -> ROW`, vạch cathode hướng ROW. Chỉ một ROW được kéo thấp mỗi lượt quét; ROW còn lại là input.

Touch dùng chung XP=D39, XM=D14, YP=D13, YM=D40. CS LCD được kéo cao khi đo, sau đó phục hồi bus. ADC 10 bit. Giá trị hiệu chuẩn tham khảo: x=136…907, y=139…942, landscape đảo trục theo panel cũ. Bản này dùng touch để chọn P1…P5 ở đáy màn hình; độ chính xác cần xác nhận trên phần cứng.

LCD đã được ghi nhận ID `0x8357` từ thanh ghi `0xBF` (`00 01 62 83 57 FF`). Driver dùng HX8357-B, MADCTL `0x28`, RGB565, INVON. Không dùng init HX8357-D. Shield cần cả 5V và 3V3 như bảng; lịch sử ghi nhận màn rất tối khi bỏ chân 3V3 POWER.

Nguồn tham khảo chỉ đọc:
- [HARDWARE tại commit đã kiểm tra](https://github.com/mihqag148/PIXEL-PRO/blob/8086533652f131a41edf0c38a80da5b109643350/docs/HARDWARE.md)
- [Sửa cấp nguồn 3V3](https://github.com/mihqag148/PIXEL-PRO/commit/b62b1c35ae1fed68810ade99b65e8d4a7d76e4c3)
- [Sửa hướng cảm ứng](https://github.com/mihqag148/PIXEL-PRO/commit/4983bb5be09308da315c25e74df9564c1536edb4)

RGB giới hạn brightness 0…80/255, mặc định 24. D15 cũng nối LED xanh onboard; không sử dụng LED onboard làm đèn trạng thái riêng. Tất cả thiết bị chung GND. Không nối nguồn 5V ngoài ngược vào VBUS PC khi chưa có mạch cách ly nguồn phù hợp.
