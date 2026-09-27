# Validation 2.4

Automated gates:
- actual ESP32-S2 compile with Arduino-ESP32 3.3.12;
- C++ model tests including 25-profile ranges;
- source guards for CDC ownership, stable HX8357-B timing, fast-storage boot, raw touch axis wiring, affine calibration, ghost filtering, PXS2 and 25-profile migration;
- .NET protocol/preset tests including schema-3→4 migration, 25 profiles, ProfilePreset, 512-action PXS2 and Power Off validation;
- real Studio window smoke render;
- self-contained win-x64 publish;
- partition/merged image validation and release SHA256.

Physical validation:
1. flash merged and cold boot five times;
2. Reset Touch;
3. enable Touch Diagnostics and tap all four corners + center;
4. calibrate TL→TR→BR→BL using hold/release;
5. verify corners, center and bottom profile strip;
6. test P1/P5/P6/P10/P21/P25 selection through Studio/actions;
7. test HID keyboard/media/mouse/PXS2 without Studio;
8. test App Mode, Power Off only on a disposable/test session;
9. verify Auto Sync prompts when PC/device keymaps differ;
10. verify GIF/icon/RGB/PC Monitor/auto screen-off and reconnect.

Green CI validates software build/tests, not the physical touch panel.
