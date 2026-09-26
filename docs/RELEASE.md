## 2.0.1 — Sửa hướng hiển thị, tăng độ ổn định LCD

- Mặc định xoay 180° so với 2.0.0 để xử lý hướng hiển thị người dùng đã báo.
- Studio 2.0.1 cho chọn hướng/lật ngang, lưu trên thiết bị và chuyển tọa độ touch đồng bộ.
- Bỏ bảng power/VCOM/gamma ép thêm; dùng trình tự init tối giản theo driver HX8357-B hiện tại của repo cũ (chỉ đọc tham khảo).
- Thêm setup/WR-low/hold cho bus i8080 và thời gian ổn định sau reset.
- Giữ pinout, partition và keymap đã lưu. App mới nhận firmware 2.0.x.

### Flash Download Tool

**Chỉ chọn PIXEL_PRO_2_merged.bin và nhập địa chỉ 0x000000.** Chọn ESP32-S2, SPI Download, DIO, 40 MHz, 4 MB. Sau FINISH, nhấn RESET/rút cắm lại USB.

App-only: chọn duy nhất PIXEL_PRO_2_app.bin tại **0x010000**, chỉ với partition v2 đã có. Không dùng cùng offset cho merged và app. Nạp merged vào offset app có thể khiến firmware không boot và LCD trắng; offset lần nạp bị lỗi của người dùng chưa được xác nhận.

Tải firmware ZIP để có hướng dẫn và script kiểm tra checksum/nạp đúng offset. Tải Studio Windows mới cùng bản; Studio 2.0.0 không chấp nhận firmware 2.0.1.

Build và kiểm thử phần mềm được CI kiểm tra trước phát hành. **Chưa xác nhận hết lỗi trên thiết bị vật lý của người dùng.**
