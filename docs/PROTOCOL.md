# PIXELPRO2 CDC protocol 2.3

Control channel uses ASCII newline framing. Host sends `id|COMMAND|...`; firmware replies `R|id|OK|payload` or `R|id|ERR|reason`. Asynchronous events begin with `E|`.

Studio asserts DTR first, then RTS. Firmware disables native-USBCDC reboot sequencing and does not gate replies on `USBCDC::operator bool()`.

| Request | Payload |
|---|---|
| HELLO | `PIXELPRO2|2.3.0|5|8|HX8357B|caps` |
| STATE | active profile, brightness, saver seconds, screen-off seconds |
| GET\|p\|k | type, code, modifiers, RGB565, label |
| SET\|p\|k\|type\|code\|modifiers\|color\|label | SET |
| PROFILE\|p | select profile 0…4 |
| RGB\|0…80 | LED brightness |
| SAVE | persist keymap/RGB |
| PANEL | display orientation 0…3 |
| DISPLAY\|mode | persist orientation |
| SAVER\|seconds | screensaver idle timeout; 0 disables |
| SCREENOFF\|seconds | 0 / 30 / 300 / 900 |
| TOUCHCAL\|START | start four-point calibration |
| TOUCHCAL\|GET | raw calibration endpoints |
| SDINFO | lazily probe SD; ABSENT or READY + MiB |
| MEDIA\|INFO | screensaver status |
| MEDIA\|DELETE | delete screensaver |
| MEDIA\|BEGIN\|size\|crc32 | start screensaver transfer |
| ICON\|DELETE\|p\|k | delete key icon |
| ICON\|BEGIN\|p\|k\|size\|crc32 | start icon transfer |
| SCRIPT\|DELETE\|p\|k | delete native HID script |
| SCRIPT\|BEGIN\|p\|k\|size\|crc32 | start PXS2 native-script transfer |
| MONITOR\|SET\|cpu\|gpu\|ram\|disk\|netKbps\|cpuTempC\|gpuTempC | render/update PC monitor |
| MONITOR\|OFF | leave PC monitor |

Events include:

- `E|KEY|k|0/1`
- `E|HOST|p|k`
- `E|PROFILE|p`
- `E|TOUCH|x|y|pressure`
- `E|CALDONE|...`
- `E|CALFAIL|...`
- `E|SCRIPTERR|reason`
- `E|MEDIAACK|received`
- `E|MEDIADONE|OK/reason`

## Binary transport

MEDIA / ICON / SCRIPT BEGIN returns `READY|512`. Host then sends raw bytes in chunks up to 512 bytes. Firmware emits a cumulative `MEDIAACK` and validates CRC32 before atomically renaming `/upload.tmp`.

CRC32 polynomial: `0xEDB88320`.

### PXG1 screensaver

- magic: `PXG1`
- uint16 LE width, height, frame count, frame delay ms
- RGB332 frame bytes
- current Studio output: 160×106, up to 1.8 MB

### PXI1 icon

- magic: `PXI1`
- uint16 LE width, height
- RGB332 pixels
- current size 48×48

### PXS2 native HID script

- bytes 0…3: ASCII `PXS2`
- bytes 4…5: uint16 LE action count
- action records follow
- max **512 actions**
- max **8192 bytes**
- stored as `/s<profile><key>.pxs`

Current PXS2 action opcodes:

1. Text: uint8 length + ASCII bytes
2. Shortcut/functional key: modifier byte + count + HID usages
3. Wait: uint16 milliseconds
4. Mouse click
5. Mouse wheel
6. Consumer/media usage
7. Select profile
8. Profile next/previous

## Key binding types

- **K**: native keyboard HID, usage 4…115 + modifier mask.
- **C**: native consumer/media HID.
- **M**: native mouse HID: left/right/middle/double-left/wheel ±.
- **S**: native PXS2 script stored in SPIFFS.
- **H**: Studio/host action; physical press emits `E|HOST|profile|key`.
- **P**: select profile 0…4.
- **D**: disabled.

## Touch

Calibration is done with the panel temporarily in orientation 0. Raw endpoints are mapped back to target coordinates x=24/455 and y=24/295, then extrapolated/constrained to 480×320 and finally passed through the selected orientation transform. Touch events include the filtered pressure score for diagnostics.

## Auto screen-off

`SCREENOFF` controls the HX8357-B display state only. It does not disable USB HID/CDC. Key, touch or roller activity calls the normal user-activity path and turns the panel back on.
