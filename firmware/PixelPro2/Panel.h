#pragma once
#include <Arduino.h>
#include <Adafruit_GFX.h>
#include <initializer_list>
#include "soc/gpio_struct.h"
#include "Pins.h"
// Independent i8080 write-only HX8357-B driver. Register values are hardware
// configuration derived from the user's working panel diagnostics, not an
// HX8357-D driver. CS must stay high while the shared touch pins are sampled.
class Panel : public Adafruit_GFX {
  void bus(uint8_t v) {
    // Only GPIO33..40 are touched; preserve every other high-bank pin.
    GPIO.out1_w1tc.val=0x1FE;
    GPIO.out1_w1ts.val=uint32_t(v)<<1;
    GPIO.out_w1tc=1UL<<Pins::wr;
    asm volatile("nop; nop; nop; nop;");
    GPIO.out_w1ts=1UL<<Pins::wr;
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
    pinMode(Pins::dc,OUTPUT);pinMode(Pins::wr,OUTPUT);
    digitalWrite(Pins::wr,HIGH);digitalWrite(Pins::dc,HIGH);
  }
  void begin() {
    restore();delay(200);
    reg(0xB0,{0,0});reg(0x01);delay(150);reg(0x28);
    reg(0x3A,{0x55});reg(0x11);delay(150);
    reg(0xD0,{0x44,0x41,0x06});reg(0xD1,{0x40,0x10});reg(0xD2,{0x05,0x12});
    reg(0xC0,{0x14,0x3B,0,2,0x11});reg(0xC5,{0x0C});reg(0xE9,{1});
    reg(0xEA,{3,0,0});reg(0xEB,{0x40,0x54,0x26,0xDB});
    reg(0xC8,{0,0x15,0,0x22,0,8,0x77,0x26,0x66,0x22,4,0});reg(0xB4,{0});
    reg(0x36,{0x28});reg(0x21);reg(0x29);delay(50);setTextWrap(false);
  }
  void drawPixel(int16_t x,int16_t y,uint16_t c) override {fillRect(x,y,1,1,c);}
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
