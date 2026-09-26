# Dependencies and provenance

Application and firmware application logic for PIXEL PRO 2.0 are maintained in this repository.

- [Arduino-ESP32](https://github.com/espressif/arduino-esp32) 3.3.12: Arduino core and USB/TinyUSB integration, LGPL-2.1 and bundled component licenses.
- [Adafruit GFX](https://github.com/adafruit/Adafruit-GFX-Library) 1.11.11: graphics primitives/font, BSD license, with Adafruit BusIO dependency.
- [Adafruit NeoPixel](https://github.com/adafruit/Adafruit_NeoPixel) 1.15.5: RGB output, LGPL-3.0.
- [.NET runtime and System.IO.Ports](https://github.com/dotnet/runtime): MIT; runtime notices distributed with the self-contained app.
- [esptool](https://github.com/espressif/esptool) 5.1.0: build/flash tooling, GPL-2.0.
- [eezbotfun/8-key-macropad](https://github.com/eezbotfun/8-key-macropad): MIT-licensed public reference used to compare user-facing workflows and feature organization (HID vs host actions, configurable key presentation, scripts/macros, display/profile behavior).

PIXEL PRO 2.0 uses its own CDC protocol and hardware-specific implementation for ESP32-S2 + MCUFRIEND/HX8357-B. No eezbotfun firmware binaries or branded assets are included. The implementation added here was written for this repository rather than copying their device protocol verbatim.

Hardware pin numbers and HX8357-B panel settings refer to the user's prior PIXEL-PRO documentation only as a read-only hardware reference; no changes are pushed to that repository.
