# Dependencies and provenance

Application and firmware application logic were written independently for PIXEL PRO 2.0.

- [Arduino-ESP32](https://github.com/espressif/arduino-esp32) 3.3.12: Arduino core and USB/TinyUSB integration, LGPL-2.1 and bundled component licenses.
- [Adafruit GFX](https://github.com/adafruit/Adafruit-GFX-Library) 1.11.11: graphics primitives/font, BSD license, with Adafruit BusIO dependency.
- [Adafruit NeoPixel](https://github.com/adafruit/Adafruit_NeoPixel) 1.15.5: RGB output, LGPL-3.0.
- [.NET runtime and System.IO.Ports](https://github.com/dotnet/runtime): MIT; runtime notices distributed with the self-contained app.
- [esptool](https://github.com/espressif/esptool) 5.1.0: build/flash tooling, GPL-2.0.

Builds obtain these dependencies from their official distributions. Upstream source and licenses are available at the links above and exact versions in the build workflow. Firmware source/build instructions remain public to permit rebuilding with modified libraries. No eezbotfun code/assets were copied; its public product workflow was an architectural reference only. Hardware pin numbers and panel register settings refer to the user's PIXEL-PRO documentation, read without modifying that repository.
