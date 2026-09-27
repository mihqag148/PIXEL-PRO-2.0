# Validation

Automated gates:

- Compile the actual sketch for **ESP32-S2** using Arduino-ESP32 3.3.12.
- C++ model tests for binding ranges, mouse/script binding types, serial overflow recovery, debounce and display orientation.
- Source guards for native USB CDC ownership, calibrated HX8357-B startup, fast-boot storage behavior, touch mapping/sampling, PXS2 native scripts and auto screen-off.
- .NET tests for protocol compatibility, binding serialization, macro validation, HID shortcut conversion, PXS2 encoding up to **512 actions/key**, auto-profile preset schema and JSON round trips.
- Off-screen construction/render of the real Studio 2.3 window.
- Publish a self-contained Windows x64 Studio.
- Verify compiled partition addresses/sizes and merged image, package firmware/Studio, and SHA256 every release asset.
- Release job depends on both build jobs. Existing tags/assets are never overwritten.

Hardware validation still required:

1. Cold boot/reconnect at least five times: HID + CDC enumerate without waiting for Studio.
2. After a **full merged flash**, verify the main UI becomes visible promptly; no SPIFFS format should block boot. First media/icon/script upload may initialize the filesystem.
3. Run four-point touch calibration, then test corners, center and profile strip with light taps. Studio logs x/y/pressure.
4. Test all 8 keys in simple keyboard HID, media HID, mouse HID and PXS2 native-script modes.
5. Verify 512-action native-script transfer, execution, delay and profile switching without Studio running.
6. Verify App Mode actions with Studio in background: URL/app/file/folder, text, shortcuts and mouse movement.
7. Verify key/profile drag-copy, Save Key, Save Profile and Save To Device.
8. Verify dynamic profile switching using foreground Windows processes.
9. Verify LCD orientation, GIF screensaver, key icons, PC Monitor and auto screen-off wake from key/touch/roller.
10. Disconnect during binary transfer and verify reconnect/retry behavior.

A green CI build proves software build/tests only; it does **not** substitute for validation on the user's physical panel/touch controller.
