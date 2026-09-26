# Studio Windows

Chọn cổng COM và **Kết nối**. App xác minh handshake PIXELPRO2 trước khi gửi cấu hình. Kết nối không tự ghi đè thiết bị hoặc bản nháp trên PC.

- **Đọc thiết bị**: đọc đủ 40 binding rồi thay bản nháp. Nội dung macro vẫn giữ ở PC vì thiết bị chỉ lưu hành động H.
- Chọn profile và ô K1…K8. Nhãn LCD dùng ASCII tối đa 12 ký tự. Chọn loại hành động và mã theo chú giải ngay trong app.
- **Gửi & lưu**: gửi 5 profile, chờ ACK từng lệnh, lưu NVS và chọn profile. Nếu bị ngắt giữa chừng, app báo lỗi; một phần cấu hình RAM có thể đã đổi nhưng chưa SAVE. Kết nối và gửi lại toàn bộ.
- **Nhập/Xuất preset**: JSON schema 2, chứa 5×8 binding và macro. Nhập không tự chạy macro hay gửi xuống thiết bị.
- Bản nháp tự lưu ở `%LOCALAPPDATA%\PixelPro2\preset.json` khi thoát hoặc lưu bản nháp.
- **Thu vào khay** giữ app chạy. Nhấp đúp icon khay để mở lại. Đóng cửa sổ bằng X sẽ thoát.

## Phím HID (K)

Mã theo USB keyboard usage: A=4, B=5…Z=29, 1=30…0=39, Enter=40, Escape=41, Tab=43, Space=44, F1=58…F12=69.
Modifier: Ctrl=1, Shift=2, Alt=4, Win=8, phải Ctrl=16, Shift=32, Alt=64, Win=128. Cộng bit khi kết hợp. Ví dụ Copy: K, code=6, modifier=1. Hoạt động không cần Studio. Tối đa 6 phím HID khác nhau giữ cùng lúc.

Media (C): volume+=233, volume−=234, mute=226, play/pause=205, next=181, previous=182, stop=183. Modifier=0.
Profile (P): code=0…4, modifier=0. Disabled (D) và Host (H): code=0, modifier=0.

## Macro PC (H)

Mỗi dòng một bước. Ví dụ:

```text
Shortcut|CTRL+L
Text|https://example.com
Shortcut|ENTER
Delay|500
```

Các loại: Text (Unicode), Shortcut (CTRL/SHIFT/ALT/WIN, A–Z, 0–9, F1–F24, ENTER/ESC/SPACE/TAB/LEFT/RIGHT/UP/DOWN/DELETE/BACKSPACE), Open (URL http/https hoặc đường dẫn đầy đủ), Delay (0…10000 ms). Tối đa 32 bước, mỗi giá trị tối đa 4096 ký tự, không hỗ trợ xuống dòng trong một bước.

Bật **Cho phép macro trên PC này** sau khi xem nội dung. Tùy chọn này không được lưu và tự tắt khi kết nối lại, nhập preset hoặc gửi cấu hình. Macro gửi vào cửa sổ đang có focus; thu Studio vào khay rồi chọn cửa sổ đích. Không chạy được vào app có quyền cao hơn. Các yêu cầu trong lúc một macro đang chạy được bỏ qua để tránh chồng phím.

Các phím K/C chạy trực tiếp trên thiết bị; macro H chỉ chạy khi app còn mở, đúng preset PC và được cho phép. Profile đổi trên thiết bị được ghi vào log; profile editor không tự thay đổi để tránh mất nội dung đang soạn.
