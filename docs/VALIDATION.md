# Validation

Automated gates:

- Compile the actual sketch for ESP32-S2 using Arduino-ESP32 3.3.12.
- C++ tests for strict numeric parsing, invalid binding types/ranges, serial overflow recovery and debounce including timer wrap.
- .NET tests for binding serialization, macro validation, shortcut parsing and preset JSON round trips.
- Publish self-contained Windows x64 app.
- Verify compiled partition addresses/sizes and merged image, package firmware/Studio, SHA256 every release asset.
- Release job depends on both build jobs. No tag/asset replacement.

Hardware validation still required (no user's board was attached during development):

1. Cold boot and reconnect at least five times: HID + CDC enumerate without waiting for Studio.
2. Check all 8 keys and held Ctrl combos; release after changing profiles; no stuck keys.
3. Roller direction, mute and rapid transitions. Swap A/B only if actual rotation is reversed.
4. Panel colors, brightness, text direction and boot stability. Register values trace back to the user's HX8357-B diagnostics; bus timing requires physical measurement if artifacts occur.
5. Touch all five profile buttons; shared-bus restoration must not corrupt pixels. Existing calibration is a starting value, not a new physical calibration.
6. Save, power cycle, read every binding, disconnect during transfers, verify reconnect and retry behavior.
7. Host macros in a normal-privilege text editor; importing a preset must not execute anything.
8. Optional SD detection and RGB ordering. PCA9546A modules are reserved but not supported in 2.0.0.

No assertion of hardware acceptance is made from a successful CI build.
