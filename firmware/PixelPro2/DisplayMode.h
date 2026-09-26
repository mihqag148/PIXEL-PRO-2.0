#pragma once
#include <stdint.h>
namespace Pixel {
// Preserve landscape MV + BGR. Bit 0 rotates 180 degrees relative to v2.0.0;
// bit 1 mirrors the landscape X axis (MY because MV exchanges the axes).
constexpr uint8_t displayMadctl(uint8_t mode) {
  return 0x28 ^ ((mode & 1) ? 0xC0 : 0) ^ ((mode & 2) ? 0x80 : 0);
}
inline void orientTouch(uint8_t mode,int& x,int& y) {
  if(mode & 1){x=479-x;y=319-y;}
  if(mode & 2)x=479-x;
}
}
