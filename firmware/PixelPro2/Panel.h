#pragma once
#include <Arduino.h>
#include <Adafruit_GFX.h>
#include <initializer_list>
#include "soc/gpio_struct.h"
#include "Pins.h"
#include "DisplayMode.h"

class Panel : public Adafruit_GFX {
  void bus(uint8_t v) {
    GPIO.out1_w1tc.val=0x1FE;
    GPIO.out1_w1ts.val=uint32_t(v)<<1;
    asm volatile("memw; .rept 16; nop; .endr" ::: "memory");
    GPIO.out_w1tc=1UL<<Pins::wr;
    asm volatile("memw; .rept 16; nop; .endr" ::: "memory");
    GPIO.out_w1ts=1UL<<Pins::wr;
    asm volatile("memw; .rept 12; nop; .endr" ::: "memory");
  }
  void command(uint8_t c){digitalWrite(Pins::dc,LOW);bus(c);digitalWrite(Pins::dc,HIGH);}
  void reg(uint8_t c,std::initializer_list<uint8_t> bytes={}) {
    digitalWrite(Pins::cs,LOW);command(c);for(auto b:bytes)bus(b);digitalWrite(Pins::cs,HIGH);
  }
  void writeWindow(int16_t x,int16_t y,int16_t w,int16_t h) {
    reg(0x2A,{uint8_t(x>>8),uint8_t(x),uint8_t((x+w-1)>>8),uint8_t(x+w-1)});
    reg(0x2B,{uint8_t(y>>8),uint8_t(y),uint8_t((y+h-1)>>8),uint8_t(y+h-1)});
    digitalWrite(Pins::cs,LOW);command(0x2C);
  }
  static uint16_t rgb332(uint8_t c) {
    uint16_t r=(c>>5)&7,g=(c>>2)&7,b=c&3;
    uint16_t r5=(r<<2)|(r>>1),g6=(g<<3)|g,b5=(b<<3)|(b<<1)|(b>>1);
    return uint16_t((r5<<11)|(g6<<5)|b5);
  }
public:
  Panel():Adafruit_GFX(480,320){}

  void restore() {
    digitalWrite(Pins::cs,HIGH);pinMode(Pins::cs,OUTPUT);
    for(auto p:Pins::data)pinMode(p,OUTPUT);
    digitalWrite(Pins::wr,HIGH);digitalWrite(Pins::dc,HIGH);
    pinMode(Pins::dc,OUTPUT);pinMode(Pins::wr,OUTPUT);
    delayMicroseconds(4);
  }

  void orientation(uint8_t mode){reg(0x36,{Pixel::displayMadctl(mode)});}
  void displayOff(){reg(0x28);}
  void displayOn(){reg(0x29);}

  void begin(uint8_t mode,uint16_t initialColor=0x0000) {
    restore();
    // Keep the panel blank throughout init. HX8357-B only needs a short delay
    // after software reset and >=120 ms after Sleep Out.
    reg(0x28);delay(5);
    reg(0xB0,{0,0});reg(0x01);delay(15);reg(0x28);
    reg(0x3A,{0x55});delay(1);reg(0x11);delay(120);
    orientation(mode);reg(0x21);
    fillScreen(initialColor);
    reg(0x29);delay(15);setTextWrap(false);
  }

  void drawPixel(int16_t x,int16_t y,uint16_t c) override{fillRect(x,y,1,1,c);}

  void fillRect(int16_t x,int16_t y,int16_t w,int16_t h,uint16_t c) override {
    if(x<0){w+=x;x=0;}if(y<0){h+=y;y=0;}
    if(x>=480||y>=320||w<=0||h<=0)return;
    if(x+w>480)w=480-x;if(y+h>320)h=320-y;
    writeWindow(x,y,w,h);
    for(int32_t i=0;i<int32_t(w)*h;++i){bus(c>>8);bus(c);}
    digitalWrite(Pins::cs,HIGH);
  }

  void drawRgb332(int16_t x,int16_t y,const uint8_t* src,uint16_t w,uint16_t h) {
    if(!src||!w||!h||x<0||y<0||x+w>480||y+h>320)return;
    writeWindow(x,y,w,h);
    for(uint32_t i=0;i<uint32_t(w)*h;i++) {
      uint16_t color=rgb332(src[i]);
      bus(color>>8);bus(color);
    }
    digitalWrite(Pins::cs,HIGH);
  }

  // Media frames are stored as 160x106 RGB332. Scale 3x while streaming the
  // frame so no full 480x320 framebuffer is required.
  void drawRgb332Scaled3(const uint8_t* src,uint16_t w,uint16_t h) {
    if(!src||!w||!h||w*3>480||h*3>320)return;
    int16_t ox=(480-int(w)*3)/2,oy=(320-int(h)*3)/2;
    writeWindow(ox,oy,w*3,h*3);
    for(uint16_t sy=0;sy<h;sy++) {
      const uint8_t* row=src+uint32_t(sy)*w;
      for(uint8_t ry=0;ry<3;ry++) {
        for(uint16_t sx=0;sx<w;sx++) {
          uint16_t c=rgb332(row[sx]);
          for(uint8_t rx=0;rx<3;rx++){bus(c>>8);bus(c);}
        }
      }
    }
    digitalWrite(Pins::cs,HIGH);
  }
};
