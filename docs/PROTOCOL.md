# PIXELPRO2 CDC protocol 2.4

ASCII newline control framing. Host: `id|COMMAND|...`. Response: `R|id|OK|payload` / `R|id|ERR|reason`. Events start with `E|`.

| Request | Payload |
|---|---|
| HELLO | `PIXELPRO2|2.4.0|25|8|HX8357B|caps` |
| KEYHASH | uint32 decimal FNV-1a of all 25×8 bindings |
| STATE | active profile, brightness, saver seconds, screen-off seconds |
| GET\|p\|k | binding, p=0…24, k=0…7 |
| SET\|p\|k\|type\|code\|modifiers\|color\|label | SET |
| PROFILE\|p | select 0…24 |
| RGB\|0…80 | brightness |
| SAVE | persist bindings |
| PANEL | display orientation |
| DISPLAY\|mode | 0…3 |
| SAVER\|seconds | 0…3600 |
| SCREENOFF\|seconds | 0 / 30 / 300 / 900 |
| TOUCHCAL\|START | begin four-point affine calibration |
| TOUCHCAL\|GET | DEFAULT or AFFINE coefficients |
| TOUCHCAL\|RESET | delete saved affine calibration |
| TOUCHDIAG\|ON/OFF | raw touch diagnostic events |
| SDINFO | lazy SD probe |
| MEDIA\|INFO / DELETE / BEGIN | GIF media |
| ICON\|DELETE / BEGIN | per-key icon |
| SCRIPT\|DELETE / BEGIN | PXS2 native script |
| MONITOR\|SET / OFF | PC monitor |

Touch events:
- `E|TOUCH|x|y|quality`
- `E|TOUCHRAW|pressed|rawX|rawY|quality|mappedX|mappedY`
- `E|CALPOINT|point|rawX|rawY`
- `E|CALWAIT|point|HOLD`
- `E|CALDONE|AFFINE|ax|bx|cx|ay|by|cy`
- `E|CALFAIL|BAD_GEOMETRY`

Other events include KEY, HOST, PROFILE, SCRIPTERR, MEDIAACK, MEDIADONE.

## Key types

- K keyboard HID
- C consumer/media HID
- M mouse HID
- S native PXS2 script
- H Studio host action
- P profile 0…24
- D disabled

## PXS2

Magic `PXS2`, uint16 action count, max 512 actions and 8192 bytes/key. Supports Text, Shortcut/Functional Key, Wait, Mouse Click, Wheel, Media, Change Profile, Profile Next/Previous.

## KEYHASH

FNV-1a 32-bit, initial 2166136261, multiplier 16777619. For every binding in profile/key order hash:
type byte, code LE16, modifiers byte, color LE16, then 13 label bytes including zero padding.
