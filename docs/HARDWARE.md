# PIXEL PRO 2.0 hardware

Board: LOLIN/WEMOS S2 Mini, ESP32-S2, 4 MB flash, 2 MB PSRAM.

| Nhóm | Dxx |
|---|---|
| ROW1, ROW2 | D1, D2 |
| COL1…COL4 | D3, D4, D5, D6 |
| Roller A, B, push | D7, D8, D21 |
| SD SCK, MISO, MOSI, CS | D9, D10, D11, D12 |
| LCD WR, DC, CS | D13, D14, D16 |
| RGB DATA | D15 |
| Expansion SCL/SDA | D17, D18 |
| Native USB D−/D+ | D19, D20 |
| LCD D0…D7 | D33…D40 |
| LCD RST | EN |
| LCD RD | 3V3 |

LCD: MCUFRIEND/HX8357-B ID 0x8357, 480×320, i8080 8-bit, RGB565. Stable init timing from v2.2.1 remains intentional.

## Resistive touch

Shared shield electrodes:
- XP = D39
- XM = D14
- YP = D13
- YM = D40

ADC resolution: 10 bit.

The important raw geometry verified from the earlier working PIXEL-PRO code is:

```text
rawX = TouchScreen tp.x, sampled on YP/D13
rawY = TouchScreen tp.y, sampled on XM/D14
screenX = map(rawY, 942, 139, 0, 479)
screenY = map(rawX, 136, 907, 0, 319)
```

Therefore rawX must **not** be directly mapped to screen X. v2.4 uses that fallback and a saved 4-point affine transform after calibration.

During touch read LCD CS is high. Shared WR/DC/D6/D7 pins are restored immediately after ADC sampling.

## Matrix

2×4 matrix, K1…K4 row 1, K5…K8 row 2. Native USB D19/D20 are never reused.

## Power

Shield requires 5V/VBUS, 3V3 POWER and common GND as on the tested hardware. Do not back-feed PC VBUS from an external 5V supply without proper power isolation.
