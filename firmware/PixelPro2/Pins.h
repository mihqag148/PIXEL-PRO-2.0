#pragma once
#include <stdint.h>
// Dxx silk-screen numbers on the user's LOLIN S2 Mini map to GPIO xx.
namespace Pins {
constexpr uint8_t rows[] = {1, 2}, cols[] = {3, 4, 5, 6};
constexpr uint8_t encoderA=7, encoderB=8, encoderPush=21;
constexpr uint8_t sdSck=9, sdMiso=10, sdMosi=11, sdCs=12;
constexpr uint8_t wr=13, dc=14, rgb=15, cs=16, scl=17, sda=18;
constexpr uint8_t data[] = {33,34,35,36,37,38,39,40};
// USB 19/20 reserved; LCD reset -> EN; RD -> 3V3. Never drive EN.
}
