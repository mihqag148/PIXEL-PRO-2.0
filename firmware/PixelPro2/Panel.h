#pragma once
#include <Arduino.h>
#include <Adafruit_GFX.h>
#include <initializer_list>
#include "soc/gpio_struct.h"
#include "Pins.h"
#include "DisplayMode.h"
// Independent i8080 write-only HX8357-B driver. Register values are hardware
// configuration derived from the user's working panel diagnostics, not an
// HX8357-D driver. CS must stay high while the shared touch pins are sampled.
class Panel : public Adafruit_GFX {
  void bus(uint8_t v) {
    // Only GPIO33..40 are touched; preserve every other high-bank pin.
    GPIO.out1_w1tc.val=0x1FE;
    GPIO.out1_w1ts.val=uint32_t(v)<<1;
    // Explicit setup, low pulse and hold intervals. The old four-NOP pulse
    // omitted setup/hold margin for the user's parallel shield and wiring.
    asm volatile("memw; .rept 16; nop; .endr" ::: "memory");
    GPIO.out_w1tc=1UL<<Pins::wr;
    asm volatile("memw; .rept 16; nop; .endr" ::: "memory");
    GPIO.out_w1ts=1UL<<Pins::wr;
    asm volatile("memw; .rept 12; nop; .endr" ::: "memory");
  }
  void command(uint8_t c) {digitalWrite(Pins::dc,LOW);bus(c);digitalWrite(Pins::dc,HIGH);}
  void reg(uint8_t c,std::initializer_list<uint8_t> bytes={}) {
    digitalWrite(Pins::cs,LOW);command(c);for(auto b:bytes)bus(b);digitalWrite(Pins::cs,HIGH);
  }
public:
  Panel():Adafruit_GFX(480,320) {}
  void restore() {
    digitalWrite(Pins::cs,HIGH);pinMode(Pins::cs,OUTPUT);
    for(auto p:Pins::data)pinMode(p,OUTPUT);
    digitalWrite(Pins::wr,HIGH);digitalWrite(Pins::dc,HIGH);
    pinMode(Pins::dc,OUTPUT);pinMode(Pins::wr,OUTPUT);
    delayMicroseconds(4);
  }
  void orientation(uint8_t mode) {reg(0x36,{Pixel::displayMadctl(mode)});}
  void begin(uint8_t mode) {
    restore();delay(1000);
    reg(0xB0,{0,0});reg(0x01);delay(150);reg(0x28);
    reg(0x3A,{0x55});delay(1);reg(0x11);delay(150);
    // The actual 0x8357 MCUFRIEND path does not force panel power/gamma.
    // Keep the user's panel defaults, as the current old-repo driver does.
    reg(0x29);delay(50);orientation(mode);reg(0x21);setTextWrap(false);
  }
  void drawPixel(int16_t x,int16_t y,uint16_t c) override {fillRect(x,y,1,1,c);}
  static uint16_t fromRgb332(uint8_t c) {
    uint16_t r=(c>>5)&7, g=(c>>2)&7, b=c&3;
    uint16_t r5=(r<<2)|(r>>1), g6=(g<<3)|g, b5=(b<<3)|(b<<1)|(b>>1);
    return uint16_t((r5<<11)|(g6<<5)|b5);
  }
  void drawRgb332Scaled3(const uint8_t* src,uint16_t w,uint16_t h) {
    if(!src||!w||!h||w*3>480||h*3>320)return;
    int16_t ox=(480-int(w)*3)/2, oy=(320-int(h)*3)/2;
    for(uint16_t y=0;y<h;y++)for(uint16_t x=0;x<w;x++)
      fillRect(ox+x*3,oy+y*3,3,3,fromRgb332(src[uint32_t(y)*w+x]));
  }
  void fillRect(int16_t x,int16_t y,int16_t w,int16_t h,uint16_t c) override {
    if(x<0){w+=x;x=0;}if(y<0){h+=y;y=0;}
    if(x>=480||y>=320||w<=0||h<=0)return;
    if(x+w>480)w=480-x;if(y+h>320)h=320-y;
    reg(0x2A,{uint8_t(x>>8),uint8_t(x),uint8_t((x+w-1)>>8),uint8_t(x+w-1)});
    reg(0x2B,{uint8_t(y>>8),uint8_t(y),uint8_t((y+h-1)>>8),uint8_t(y+h-1)});
    digitalWrite(Pins::cs,LOW);command(0x2C);
    for(int32_t i=0;i<int32_t(w)*h;++i){bus(c>>8);bus(c);}
    digitalWrite(Pins::cs,HIGH);
  }
};
