# PIXELPRO2 CDC protocol 2.0

ASCII newline framing; optional CR before LF. Host sends `id|COMMAND|...` with id 1…65535. Firmware replies `R|id|OK|payload` or `R|id|ERR|reason`. Asynchronous messages start `E|`. One request at a time; 5-second host timeout. Maximum received line 191 bytes; longer/NUL frames are discarded to newline and return `R|0|ERR|FRAME`. No legacy protocol fallback.

| Request | Payload |
|---|---|
| HELLO | PIXELPRO2, version, profile/key counts, panel, caps separated by pipes |
| STATE | active profile 0…4, brightness 0…80 |
| GET\|p\|k | type, code, modifiers, RGB565 decimal, label |
| SET\|p\|k\|type\|code\|modifiers\|color\|label | SET |
| PROFILE\|p | PROFILE |
| RGB\|0…80 | RGB |
| SAVE | SAVED after successful NVS write |
| SDINFO | ABSENT or READY followed by card MiB |
| PANEL (2.0.1+) | orientation mode 0…3 |
| DISPLAY\|mode (2.0.1+) | DISPLAY after applying and persisting orientation |

Display modes: 0=2.0.0 orientation; 1=180-degree rotation (new default); 2=horizontal mirror of mode 0; 3=horizontal mirror of mode 1. Touch coordinates follow the same transform. Orientation is stored separately from the existing binding blob, so upgrading retains bindings. Studio 2.0.1 accepts 2.0.x HELLO versions; older Studio 2.0.0 requires an update for newer firmware.

`SET` modifies RAM, `SAVE` persists all bindings/brightness in one NVS blob. Profile selection is session-only, startup is P1. On profile change all HID keys are released; held physical keys remain suppressed until released. On keymap edits held keys retain their original binding until release.

Types and limits: K=usage 4…115/modifier 0…255; C=181,182,183,205,226,233,234 with modifier 0; P=0…4 with modifier 0; H/D=code 0/modifier 0. Labels are printable ASCII excluding pipe, max 12 bytes. GET retains empty labels.

Events: `E|KEY|k|0/1`, `E|HOST|p|k`, `E|PROFILE|p`, `E|TOUCH|x|y`. Firmware sends HOST only on the physical press edge of H. It never accepts a serial command to execute an arbitrary PC macro. App resolves H using its local preset, only while explicitly enabled for that session.
