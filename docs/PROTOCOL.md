# PIXELPRO2 CDC protocol 2.2

Control channel uses ASCII newline framing. Host sends `id|COMMAND|...`; firmware replies `R|id|OK|payload` or `R|id|ERR|reason`. Asynchronous events begin with `E|`. Studio 2.2 asserts both DTR and RTS because Arduino-ESP32 native USBCDC marks the port connected only in that line state; firmware disables USBCDC reboot sequencing so this does not trigger bootloader reset.

| Request | Payload |
|---|---|
| HELLO | `PIXELPRO2|2.2.0|5|8|HX8357B|caps` |
| STATE | active profile, brightness, saver seconds |
| GET\|p\|k | type, code, modifiers, RGB565, label |
| SET\|p\|k\|type\|code\|modifiers\|color\|label | SET |
| PROFILE\|p | select profile 0…4 |
| RGB\|0…80 | LED brightness |
| SAVE | persist keymap/RGB to NVS |
| PANEL | display orientation 0…3 |
| DISPLAY\|mode | persist orientation |
| SAVER\|seconds | idle timeout; 0 disables |
| TOUCHCAL\|START | start four-point calibration |
| TOUCHCAL\|GET | raw calibration endpoints |
| SDINFO | ABSENT or READY + MiB |
| MEDIA\|INFO | screensaver status |
| MEDIA\|DELETE | remove screensaver |
| MEDIA\|BEGIN\|size\|crc32 | begin raw screensaver transfer |
| ICON\|DELETE\|p\|k | remove key icon |
| ICON\|BEGIN\|p\|k\|size\|crc32 | begin raw icon transfer |
| MONITOR\|SET\|cpu\|gpu\|ram\|disk\|netKbps\|cpuTempC\|gpuTempC | render/update PC monitor fullscreen |
| MONITOR\|OFF | leave PC monitor and restore key UI |

Events: `E|KEY|k|0/1`, `E|HOST|p|k`, `E|PROFILE|p`, `E|TOUCH|x|y`, `E|CALDONE|...`, `E|CALFAIL|...`, `E|MEDIAACK|received`, `E|MEDIADONE|OK/reason`.

## Binary transfer

After a successful MEDIA/ICON BEGIN reply (`READY|512`), the host sends raw bytes in chunks up to 512 bytes. Firmware pauses LCD/touch work during transfer, writes the chunk to SPIFFS, updates CRC32 and emits a cumulative `MEDIAACK`. Final data is validated before an atomic rename from `/upload.tmp`.

Screensaver package `PXG1`:
- 4 bytes magic
- little-endian uint16 width, height, frame count, delay ms
- RGB332 frame bytes
- Studio currently emits 160×106 frames, <=1.8 MB

Icon package `PXI1`:
- 4 bytes magic
- little-endian uint16 width, height
- RGB332 pixels
- currently fixed to 48×48

CRC32 uses polynomial 0xEDB88320.

## Key types

- K: USB keyboard usage 4…115, modifier mask 0…255.
- C: consumer codes 181, 182, 183, 205, 226, 233, 234.
- H: host action; physical press generates `E|HOST|profile|key`.
- P: switch profile 0…4.
- D: disabled.

Host macros are never sent from an arbitrary serial command. Studio resolves H actions from its local preset only when **Cho phép macro trên PC này** is enabled.

## PC monitor

`MONITOR|SET` accepts CPU/GPU/RAM/disk percentages 0…100, network 0…9999 kbps, and CPU/GPU temperatures 0…125 °C. Temperature 0 means unavailable and is rendered as `--C`. The firmware keeps HID scanning active while the full-screen dashboard is visible. `MONITOR|OFF` restores the normal key tiles.
