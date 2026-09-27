## 2.4.0 — Touch affine rebuild + 25 profiles + Studio parity

### Touch root-cause fix

v2.3 mapped the resistive axes incorrectly for the real MCUFRIEND shield. The verified hardware mapping from the earlier PIXEL-PRO implementation is:

- rawX sampled from YP / D13
- rawY sampled from XM / D14
- screen X = rawY reversed
- screen Y = rawX

v2.4 ports that geometry back and replaces the old linear calibration with a **4-point affine solve**. Calibration no longer depends on the unreliable pressure test that caused only one corner to register.

Touch filtering now requires 3 coherent press polls, 4 release misses, tighter normal-sample stability, median calibration samples, and supports live raw diagnostics.

Studio adds **Reset Touch** and **Touch Diagnostics**. Calibration requires: hold each target briefly, release fully, then move to the next target.

### 25 profiles

- 25 profiles × 8 keys.
- migrates old 5-profile NVS config and schema-2/3 PC presets.
- profile actions and Auto Profile support P1…P25.
- physical LCD profile strip displays banks of five.

### Studio 2.4

- 25-profile selector.
- Profile & Key Selection + Device List left; Configuration right.
- drag/drop actions and key/profile copy.
- 512 actions/key.
- HID Mode / App Mode per key.
- SAVE KEY / SAVE PROFILE / SAVE TO DEVICE.
- Import/Export full preset and individual Profile.
- Auto Profile.
- **Auto Sync PC ↔ device** via KEYHASH prompt.
- Getting Started + HID explanation.
- Power Off Computer.
- USB Safe Mode behavior by design (no mass-storage interface).
- PC Monitor, GIF, icons, RGB, auto screen-off retained.

### Boot / USB / LCD

Keeps stable HX8357-B timing and v2.3 fast-storage boot path. Native USB CDC/HID handshake fix is retained.

### Flash

Use **PIXEL_PRO_2_merged.bin** at **0x000000** with ESP32-S2 / DIO / 40 MHz / 4 MB.

After flashing, use the matching Studio 2.4 and run:
1. Connect.
2. Display & Media → Reset Touch.
3. Touch Diagnostics ON and verify all four corners produce changing raw values.
4. Touch Diagnostics OFF.
5. Calibrate Touch: hold/release TL → TR → BR → BL.

CI release requires model/source tests, real ESP32-S2 compile, merged image verification, app tests, UI smoke and self-contained Windows publish.
