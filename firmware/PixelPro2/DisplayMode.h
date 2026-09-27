#pragma once
#include <stdint.h>
namespace Pixel {
// This HX8357-B/MCUFRIEND panel is physically mounted so the old 0x28 base
// appears as "180 + horizontal mirror". Use 0x68 as the calibrated landscape
// hardware origin. Modes remain relative UI transforms: rotate 180, mirror X.
constexpr uint8_t displayMadctl(uint8_t mode) {
  return 0x68 ^ ((mode & 1) ? 0xC0 : 0) ^ ((mode & 2) ? 0x80 : 0);
}
inline void orientTouch(uint8_t mode,int& x,int& y) {
  if(mode & 1){x=479-x;y=319-y;}
  if(mode & 2)x=479-x;
}
}
