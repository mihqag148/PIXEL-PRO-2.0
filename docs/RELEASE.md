## 2.1.0 — COM fix + eezbotfun-style feature expansion

- **Fix lỗi Studio không kết nối COM:** Arduino-ESP32 native `USBCDC` only reports connected when DTR+RTS are asserted. Studio now uses the correct line state, retries HELLO, and can auto-probe COM ports. Firmware calls `enableReboot(false)` so line-state changes do not trigger bootloader reboot.
- Expanded host macros: Unicode text, shortcuts, app/file/folder/URL launch, delay, mouse move/click/wheel, KeyDown/KeyUp.
- Four-point touch calibration with NVS persistence and orientation-aware coordinate transform.
- GIF screensaver pipeline: PC-side GIF decode/scale → RGB332 160×106 → 512-byte ACK/CRC transfer → SPIFFS → direct HX8357-B playback.
- Custom per-key icons: image → RGB332 48×48 → ACK/CRC transfer → per-profile/per-key SPIFFS asset → physical tile rendering.
- Saver timeout control, media status/delete, improved device status/logging and Studio auto-connect.
- Existing 5×8 keymap, HID/media, RGB, SD, orientation and pinout remain intact.
- Workflow builds real ESP32-S2 firmware and a self-contained Windows x64 Studio; release is created only after both jobs are green.

### Flash

For Flash Download Tool use **PIXEL_PRO_2_merged.bin at 0x000000** (ESP32-S2, DIO, 40 MHz, 4 MB). App-only `PIXEL_PRO_2_app.bin` remains at **0x010000** for an existing v2 partition layout.

Full merged flash can reset NVS and SPIFFS, so it can clear saved bindings, touch calibration, GIF and icons. Studio preset JSON can be exported separately.

### Compatibility / provenance

The user workflow and feature organization were compared with the MIT-licensed `eezbotfun/8-key-macropad` project. PIXEL PRO 2.0 keeps its own protocol and implementation tailored to the user's ESP32-S2 + MCUFRIEND/HX8357-B hardware; no upstream binaries or branded assets are redistributed.

CI confirms source build/test integrity. LCD color/timing, resistive-touch raw ranges and physical USB behavior still need confirmation on the actual unit after flashing.
