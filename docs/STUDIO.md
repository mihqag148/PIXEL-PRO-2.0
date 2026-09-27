# Studio Windows 2.3

Studio 2.3 reorganizes PIXEL PRO 2.0 around the same workflow style users expect from modern Stream-Deck/macropad configurators: choose a profile/key, build an ordered action sequence, choose HID Mode or App Mode, then save only the key/profile or synchronize the entire device.

## Layout / UX

Left pane:

- **Profile & Key Selection**: 5 profiles × 8 keys.
- Drag one key onto another to copy its binding/action sequence.
- Drag one profile onto another to copy all 8 bindings/action sequences.
- Key icon upload/delete and per-key RGB color.
- **Device List** with Refresh, Auto Find, Connect, Disconnect, Read and Sync / Save.

Right pane:

- **Key Configuration**: draggable Action toolbox + ordered Action Sequence.
- **Display & Media**: LCD orientation, touch calibration, GIF, RGB, PC Monitor and auto screen-off.
- **Auto Profile**: map foreground Windows process names to profiles.
- **Log**: CDC/device/macro diagnostics.

Header provides Import, Export, Firmware Releases and Light/Dark mode.

## Action toolbox

Studio 2.3 exposes separate actions instead of requiring users to manually type macro syntax:

- Access Website
- Launch APP
- Open Folder
- Open File
- Input Text
- Shortcut
- Wait
- Mouse Move
- Mouse Click
- Mouse Wheel
- Media Control
- Change Profile
- Functional Key
- Device Control

Actions can be dragged/double-clicked into the sequence and reordered/deleted. A key supports up to **512 actions**.

## HID Mode vs App Mode

**HID Mode** runs without Studio when every action in the sequence can be encoded by the device:

- Text
- Shortcut / Functional Key
- Wait
- Mouse Click
- Mouse Wheel
- Media Control
- Change Profile
- Profile Next / Previous

Simple one-action keys are stored directly as native keyboard/media/mouse/profile bindings. Longer HID sequences are compiled by Studio into a **PXS2** native script and stored in SPIFFS.

**App Mode** requires Studio running and is used for Windows-side actions such as:

- Website / launch app / open file / open folder
- Mouse Move
- PC Monitor toggle
- other host-only sequences

The physical device still remains a normal HID keyboard/media/mouse device.

## Save scope

- **SAVE KEY**: send only the selected key (plus its PXS2 script if needed).
- **SAVE PROFILE**: send the selected profile.
- **SAVE TO DEVICE**: synchronize all 5 × 8 keys plus RGB, screensaver timeout and display sleep setting.
- **Save Draft**: save the local schema-3 preset only.

## Dynamic / Auto Profile

The Auto Profile tab can bind a Windows foreground process name to Profile 1…5. **Add current app** captures the currently active process. When enabled and PIXEL PRO is connected, Studio checks the foreground process and switches the device profile when a matching rule becomes active.

## Touch

Touch calibration is still four points: top-left → top-right → bottom-right → bottom-left.

v2.3 changes the physical sampling path:

- median-of-5 ADC samples,
- 8 ms scan interval,
- lower valid pressure threshold,
- calibration maps the raw calibration values back to the actual on-screen target coordinates (24…455 / 24…295) rather than incorrectly treating them as the panel edges,
- touch debug events include x, y and pressure.

## Faster boot

A full merged flash leaves the SPIFFS partition blank. Older builds used `SPIFFS.begin(true)` during setup, which could format the filesystem before the UI became usable. v2.3:

- renders the UI before optional storage work,
- mounts SPIFFS without auto-format at boot,
- formats only on the first upload if needed,
- probes optional SD only when requested,
- shortens HX8357-B startup waits while retaining the required Sleep-Out delay.

## Display / Media

- GIF screensaver: 160×106 RGB332 streamed 3× to the 480×320 panel.
- 48×48 per-key RGB332 icons.
- Full-screen PC Monitor.
- Auto screen-off: **Always On / 30 seconds / 5 minutes / 15 minutes**. HID stays active and key/touch/roller activity wakes the LCD.
- Orientation 0…3 uses the calibrated landscape base MADCTL `0x68`.

## Connection

Studio opens native USB CDC by asserting DTR first and RTS second. Firmware disables Arduino-ESP32 CDC reboot sequencing and owns CDC interface 0 through its explicit `USBCDC usbLink`.

## Current parity boundary

Studio 2.3 intentionally moves much closer to the public eezbotfun workflow, but it is not claimed as a byte-for-byte or feature-for-feature clone. Features not implemented in this release include simultaneous multi-device sessions, their community preset gallery, integrated SMTC Now Playing/album-art plugin, their custom `cus` display protocol and ESP-NOW wireless mode. PIXEL PRO 2.0 retains its own protocol and hardware-specific implementation.
