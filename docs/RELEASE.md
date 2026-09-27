## 2.3.0 — Touch + Fast Boot + eez-style Studio overhaul

### Touch

- Median-of-5 ADC sampling thay cho single sample.
- Touch scan 25 ms → **8 ms**.
- Pressure detection mới nhạy hơn và event debug trả thêm pressure.
- Sửa lỗi calibration thực tế: target nằm ở x=24/455, y=24/295 nhưng firmware cũ map raw endpoints về mép 0/479 và 0/319. v2.3 map đúng về vị trí target rồi mới extrapolate/constrain ra toàn màn.
- Giữ orientation transform đồng bộ với LCD.

### Fast boot sau merged flash

- Không còn auto-format SPIFFS trong `setup()`.
- UI được render trước storage optional.
- SPIFFS mount bằng `begin(false)`; format chỉ khi upload media/icon/script lần đầu cần filesystem.
- microSD được probe lazy qua `SDINFO`.
- Rút ngắn HX8357-B startup delays, vẫn giữ >=120 ms sau Sleep Out.
- LCD vẫn Display OFF → init/orientation/clear black → Display ON.

### Native HID

- Thêm **USB Mouse HID**.
- Binding mới:
  - K keyboard
  - C consumer/media
  - M mouse
  - S native script
  - H Studio/host
  - P profile
  - D disabled
- **PXS2 native script**: tối đa **512 actions/key**, 8192 bytes/key.
- Native script hỗ trợ Text, Shortcut/Functional Key, Wait, Mouse Click, Wheel, Media, Change Profile và Profile Next/Previous.
- Các action Windows-only tiếp tục chạy qua App Mode.

### Studio 2.3

UI được viết lại theo workflow gần eezbotfun hơn:

- Profile & Key Selection bên trái.
- Device List bên trái.
- Action toolbox + Action Sequence + Parameters bên phải.
- Drag/drop action.
- Drag key → key để copy.
- Drag profile → profile để copy.
- **HID Mode / App Mode theo từng key**.
- **SAVE KEY / SAVE PROFILE / SAVE TO DEVICE**.
- tối đa 512 actions/key.
- Dynamic / Auto Profile theo foreground Windows process.
- key icon + RGB color.
- Import/Export preset schema 3.
- Light/Dark mode.
- Display & Media / Auto Profile / Log tabs.
- Auto screen-off: Always On, 30s, 5m, 15m.
- PC Monitor, GIF, touch calibration và existing v2.2.1 USB handshake được giữ.

### eezbotfun parity

Đã đối chiếu public MIT project `eezbotfun/8-key-macropad` và đưa các workflow chính vào implementation riêng của PIXEL PRO 2.0.

Không tuyên bố 1:1 toàn bộ trong 2.3.0. Chưa có: simultaneous multi-device sessions, community preset gallery, SMTC Now Playing/album art, `cus` custom-display protocol và ESP-NOW wireless.

### Flash

Full install/recovery:

- **PIXEL_PRO_2_merged.bin**
- offset **0x000000**
- ESP32-S2
- DIO
- 40 MHz
- 4 MB

App-only **PIXEL_PRO_2_app.bin** ở **0x010000** chỉ khi thiết bị đã có partition v2.

Sau full flash đầu tiên, v2.3 không format SPIFFS trong boot. Filesystem chỉ được tạo khi lần đầu upload GIF/icon/native script nếu cần.

### Validation

Release chỉ được tạo khi:

- firmware model/source guards xanh,
- actual ESP32-S2 compile xanh,
- partition/merged verification xanh,
- app protocol/model tests xanh,
- real Studio 2.3 UI smoke xanh,
- self-contained Windows publish/package xanh.

Hardware vẫn cần user xác nhận touch sensitivity, boot perception và LCD/touch behavior trên panel thật.
