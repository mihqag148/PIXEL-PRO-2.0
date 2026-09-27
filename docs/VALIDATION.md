# Validation 2.5

Automated gates:
- actual ESP32-S2 compile with Arduino-ESP32 3.3.12;
- C++ model tests including 25-profile ranges;
- source guards for CDC ownership, stable HX8357-B timing, fast-storage boot, raw touch axis wiring, affine calibration, ghost filtering, PXS2, 25-profile migration and named-pipe plugin separation;
- .NET protocol/preset tests including schema-3→4 migration, 25 profiles, ProfilePreset, 512-action PXS2, Power Off and plugin JSON envelopes;
- real Studio window smoke render;
- self-contained win-x64 Studio publish;
- standalone PC Monitor plugin publish;
- standalone Music/SMTC plugin publish;
- partition/merged image validation and release SHA256.

Physical validation:
1. flash merged and cold boot five times;
2. Reset Touch;
3. enable Touch Diagnostics and tap all four corners + center;
4. calibrate TL→TR→BR→BL using hold/release;
5. verify corners, center and bottom profile strip;
6. test P1/P5/P6/P10/P21/P25 selection through Studio/actions;
7. test HID keyboard/media/mouse/PXS2 with Studio closed;
8. test App Mode while Studio is minimized to tray;
9. verify Auto Sync prompts when PC/device keymaps differ;
10. connect two PIXEL PRO devices and switch active COM session without dropping the other;
11. enable PC Monitor plugin and verify telemetry arrives through named pipe;
12. enable Music plugin and verify SMTC title/artist/full-screen mode;
13. test Preset Gallery, offline icon preview, Save Key/Profile/Device icon sync;
14. verify GIF/RGB/auto screen-off and reconnect.

Green CI validates software build/tests, not the physical touch panel.
