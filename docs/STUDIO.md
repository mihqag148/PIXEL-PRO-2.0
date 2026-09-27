# Studio Windows 2.2.1

Studio 2.2 is the desktop configurator/background host for PIXEL PRO 2.0.

## Kết nối

- **Tự tìm phím** quét các COM hiện có, mở từng cổng với **DTR trước rồi RTS**, retry HELLO và chỉ giữ cổng trả về đúng `PIXELPRO2`.
- Firmware tắt native-USBCDC reboot-by-line-state để việc mở/đóng Studio không đá ESP32-S2 vào bootloader.
- Có thể chọn COM thủ công bằng **Kết nối**.
- Mất COM sẽ tắt quyền chạy macro host và ghi lỗi vào log.

## PC Monitor fullscreen

**PC Monitor** chuyển LCD sang trang giám sát toàn màn hình và cập nhật mỗi giây qua CDC. Studio dùng LibreHardwareMonitor 0.9.6 để đọc CPU/GPU load + nhiệt độ khi sensor có sẵn; RAM, disk và network có fallback bằng Windows/.NET API. Phím HID, media key và host macro vẫn hoạt động trong lúc trang monitor hiển thị.

Nhấn **PC Monitor** lần nữa để gửi `MONITOR|OFF` và quay lại giao diện phím. Mất COM sẽ tự tắt monitor ở Studio. Sensor không đọc được sẽ hiện 0% hoặc `--C` thay vì làm app lỗi.

## Keymap / profile

- 5 profile × 8 key.
- K = keyboard HID; C = media HID; H = macro chạy qua Studio; P = chuyển profile; D = disabled.
- Nhãn tối đa 12 ASCII, RGB565 từng phím, brightness 0…80.
- **Đọc thiết bị** đọc đủ 40 binding.
- **Gửi & lưu** gửi đủ 40 binding + RGB + saver timeout, sau đó SAVE.
- Preset JSON schema 2 lưu tại file tùy chọn và bản nháp local `%LOCALAPPDATA%\PixelPro2\preset.json`.

## Macro host

Mỗi dòng là một step:

```text
Open|C:\Tools\app.exe
Delay|500
Shortcut|CTRL+SHIFT+S
Text|hello
MouseMove|20,-10
MouseClick|LEFT
Wheel|-120
KeyDown|CTRL
KeyUp|CTRL
```

Supported: Text, Shortcut, Open (URL/app/file/folder), Delay 0…30000ms, MouseMove, MouseClick LEFT/RIGHT/MIDDLE/DOUBLELEFT, Wheel, KeyDown, KeyUp. Tối đa 64 step. Held keys are released in `finally` if a macro is interrupted.

H actions require Studio running and **Cho phép macro trên PC này** enabled. K/C actions remain native HID and do not need Studio.

## Touch

**Calibrate touch** starts an on-device four-point wizard. Touch the crosshairs in this order: top-left, top-right, bottom-right, bottom-left. Firmware validates spans, stores raw endpoints in NVS, restores current orientation and applies the same transform to touch coordinates.

## GIF screensaver

**Tải GIF** decodes on the PC, scales to 160×106 RGB332 and downsamples frame count only if necessary to stay under the flash budget. Transfer uses 512-byte cumulative ACK + CRC32. Firmware stores `/screensaver.pxg` and scales each RGB332 frame 3× to the 480×320 panel without a full-size framebuffer.

**Saver(s)** controls idle time; 0 disables the saver. Key/touch/encoder/host activity exits the saver.

## Icon phím

Select a profile and K1…K8, then choose **Tải icon phím**. PNG/JPG/BMP/GIF are fitted into a 48×48 RGB332 icon and uploaded with the same ACK/CRC transport. Icons are stored in flash per profile/key and render directly in the physical key tile. **Xóa icon phím** restores the text-only tile.

## Display

Orientation 0…3 is persisted separately from keymap. Bản 2.2.1 dùng gốc landscape đã hiệu chỉnh cho panel thực tế (`0x68`) và bỏ giá trị orientation cũ của 2.2.0 trong lần migrate đầu. RGB, touch, media và key icons dùng cùng transform.
