#include <Arduino.h>
#include <USB.h>
#include <USBCDC.h>
#include <USBHIDKeyboard.h>
#include <USBHIDConsumerControl.h>
#include <Preferences.h>
#include <SPI.h>
#include <SD.h>
#include <Adafruit_NeoPixel.h>
#include "Model.h"
#include "Panel.h"

USBCDC usbLink;
USBHIDKeyboard keyboard;
USBHIDConsumerControl media;
Preferences prefs;
Panel panel;
Adafruit_NeoPixel leds(8,Pins::rgb,NEO_GRB+NEO_KHZ800);
struct Configuration {
  uint32_t magic;
  Pixel::Binding bindings[Pixel::Profiles][Pixel::Keys];
  uint8_t brightness;
};
Configuration config{};
uint8_t profile=0;
Pixel::Debounce keys[8],push;
bool suppressed[8]{};
Pixel::Binding held[8]{};
Pixel::LineBuffer<192> input;
bool displayDirty=true,sdReady=false;
uint8_t dirtyTiles=255;
uint32_t lastScan=0,lastTouch=0,lastInput=0;
Pixel::Debounce touch;
uint16_t consumerHeld=0;
uint32_t consumerUntil=0;

void defaults() {
  memset(&config,0,sizeof(config));config.magic=0x50583201;config.brightness=24;
  for(int p=0;p<Pixel::Profiles;++p)for(int k=0;k<8;++k) {
    auto& b=config.bindings[p][k];b.type='K';b.code=4+k;b.color=0x04BF;
    snprintf(b.label,sizeof(b.label),"Key %c",'A'+k);
  }
}
void loadConfig() {
  defaults();Configuration saved{};
  if(prefs.getBytesLength("config")!=sizeof(saved))return;
  prefs.getBytes("config",&saved,sizeof(saved));
  if(saved.magic!=config.magic||saved.brightness>80)return;
  for(auto& page:saved.bindings)for(auto& b:page)if(!Pixel::valid(b))return;
  config=saved;
}
void emit(const String& text) {if(usbLink)usbLink.println(text);}
void reportKeys() {
  KeyReport report{};int count=0;
  for(int i=0;i<8;++i)if(keys[i].stable&&!suppressed[i]&&held[i].type=='K') {
    report.modifiers|=held[i].modifiers;
    uint8_t code=held[i].code;bool duplicate=false;
    for(int j=0;j<count;++j)if(report.keys[j]==code)duplicate=true;
    if(!duplicate&&count<6)report.keys[count++]=code;
  }
  keyboard.sendReport(&report);
}
void selectProfile(int p) {
  profile=p;
  for(int k=0;k<8;++k) suppressed[k]=keys[k].stable;
  keyboard.releaseAll();media.release();consumerHeld=0;
  displayDirty=true;dirtyTiles=255;
  emit("E|PROFILE|"+String(profile));
}
void consumer(uint16_t code) {media.press(code);consumerHeld=code;consumerUntil=millis()+35;}
void activate(const Pixel::Binding& b,int key,bool down) {
  if(!down)return;
  if(b.type=='C')consumer(b.code);
  if(b.type=='P')selectProfile(b.code);
  if(b.type=='H')emit("E|HOST|"+String(profile)+"|"+String(key));
}
void scanKeys(uint32_t now) {
  if(uint32_t(now-lastScan)<1)return;lastScan=now;
  bool changed=false;
  for(int r=0;r<2;++r) {
    digitalWrite(Pins::rows[r],LOW);pinMode(Pins::rows[r],OUTPUT);delayMicroseconds(5);
    for(int c=0;c<4;++c) {
      int k=r*4+c;
      if(keys[k].update(digitalRead(Pins::cols[c])==LOW,now)) {
        changed=true;dirtyTiles|=1<<k;lastInput=now;
        if(keys[k].stable){held[k]=config.bindings[profile][k];activate(held[k],k,true);}
        else suppressed[k]=false;
        emit("E|KEY|"+String(k)+"|"+String(keys[k].stable?1:0));
      }
    }
    pinMode(Pins::rows[r],INPUT);
  }
  if(changed)reportKeys();
  if(push.update(digitalRead(Pins::encoderPush)==LOW,now)&&push.stable)consumer(0xE2);
  static uint8_t previous=3;static int8_t accumulator=0;
  static const int8_t transitions[16]={0,-1,1,0,1,0,0,-1,-1,0,0,1,0,1,-1,0};
  uint8_t state=(digitalRead(Pins::encoderA)<<1)|digitalRead(Pins::encoderB);
  accumulator+=transitions[(previous<<2)|state];previous=state;
  if(accumulator>=4){consumer(0xE9);accumulator=0;}
  if(accumulator<=-4){consumer(0xEA);accumulator=0;}
}
// The X-/Y+ electrodes are ADC capable on the user's board. The other two
// electrodes are used only as digital drivers. Restore every shared LCD pin.
void scanTouch(uint32_t now) {
  if(uint32_t(now-lastTouch)<30)return;lastTouch=now;
  constexpr int xp=39,xm=14,yp=13,ym=40;
  digitalWrite(Pins::cs,HIGH);
  pinMode(yp,INPUT);pinMode(ym,INPUT);
  pinMode(xp,OUTPUT);digitalWrite(xp,HIGH);pinMode(xm,OUTPUT);digitalWrite(xm,LOW);
  delayMicroseconds(30);int rx=1023-analogRead(yp);
  pinMode(xp,INPUT);pinMode(xm,INPUT);
  pinMode(yp,OUTPUT);digitalWrite(yp,HIGH);pinMode(ym,OUTPUT);digitalWrite(ym,LOW);
  delayMicroseconds(30);int ry=1023-analogRead(xm);
  pinMode(xp,OUTPUT);digitalWrite(xp,LOW);pinMode(ym,OUTPUT);digitalWrite(ym,HIGH);
  pinMode(xm,INPUT);pinMode(yp,INPUT);delayMicroseconds(30);
  int z1=analogRead(xm),z2=analogRead(yp);
  panel.restore();
  // Resistance estimate for the measured 300-ohm X plate. Accept the two
  // divider polarities found on MCUFRIEND-compatible shields.
  int pressure=z1>0?int((int64_t(abs(z2-z1))*rx*300)/(z1*1024)):0;
  bool down=pressure>=200&&pressure<=1000&&rx>=100&&rx<=970&&ry>=100&&ry<=970;
  if(touch.update(down,now)&&touch.stable) {
    int x=constrain(map(ry,942,139,0,479),0,479);
    int y=constrain(map(rx,136,907,0,319),0,319);
    // Touch is navigation only: avoids stray touches typing on the PC.
    if(y>=280)selectProfile(constrain(x/96,0,4));
    emit("E|TOUCH|"+String(x)+"|"+String(y));
  }
}
void render() {
  if(displayDirty) {
    panel.fillScreen(0x0843);panel.setTextSize(2);panel.setTextColor(0xFFFF);
    panel.setCursor(12,10);panel.print("PIXEL PRO 2.0");
    for(int p=0;p<5;++p) {
      panel.fillRect(p*96+2,282,92,36,p==profile?0x04BF:0x18C6);
      panel.setCursor(p*96+13,292);panel.print("P");panel.print(p+1);
    }
    displayDirty=false;
  }
  // Render a single changed tile per loop, limiting input latency.
  for(int k=0;k<8;++k)if(dirtyTiles&(1<<k)) {
    dirtyTiles&=~(1<<k);auto& b=config.bindings[profile][k];
    int x=(k%4)*120+4,y=(k/4)*112+48;
    panel.fillRect(x,y,112,104,keys[k].stable?0xFFFF:0x18C6);
    panel.fillRect(x,y,112,4,b.color);
    panel.setTextColor(keys[k].stable?0x0843:0xFFFF);panel.setTextSize(2);
    panel.setCursor(x+10,y+20);panel.print(k+1);
    panel.setTextSize(1);panel.setCursor(x+8,y+64);panel.print(b.label);
    static const uint8_t order[]={0,1,2,3,7,6,5,4};
    leds.setPixelColor(order[k],leds.Color(((b.color>>11)&31)*8,((b.color>>5)&63)*4,(b.color&31)*8));
    leds.setBrightness(config.brightness);leds.show();break;
  }
}
void request(char* line) {
  char* tokens[12]{};int n=0;char* cursor=line;
  // Preserve empty fields, and reject surplus tokens.
  do {if(n==12){emit("R|0|ERR|FRAME");return;}tokens[n++]=cursor;
    cursor=strchr(cursor,'|');if(cursor)*cursor++=0;
  } while(cursor);
  uint32_t id=0;
  if(n<2||!Pixel::number(tokens[0],65535,id)){emit("R|0|ERR|ID");return;}
  String prefix="R|"+String(id)+"|";
  auto ok=[&](const String& s){emit(prefix+"OK|"+s);};
  auto error=[&](const char* s){emit(prefix+"ERR|"+s);};
  String cmd=tokens[1];uint32_t p=0,k=0,v=0,m=0,color=0;
  if(cmd=="HELLO"&&n==2){ok("PIXELPRO2|2.0.0|5|8|HX8357B|HID,CDC,RGB,TOUCH,SD");return;}
  if(cmd=="STATE"&&n==2){ok(String(profile)+"|"+String(config.brightness));return;}
  if(cmd=="PROFILE"&&n==3&&Pixel::number(tokens[2],4,p)){selectProfile(p);ok("PROFILE");return;}
  if(cmd=="GET"&&n==4&&Pixel::number(tokens[2],4,p)&&Pixel::number(tokens[3],7,k)) {
    auto& b=config.bindings[p][k];
    ok(String(b.type)+"|"+String(b.code)+"|"+String(b.modifiers)+"|"+String(b.color)+"|"+b.label);return;
  }
  if(cmd=="SET"&&n==9&&Pixel::number(tokens[2],4,p)&&Pixel::number(tokens[3],7,k)
      &&strlen(tokens[4])==1&&Pixel::number(tokens[5],65535,v)&&Pixel::number(tokens[6],255,m)
      &&Pixel::number(tokens[7],65535,color)&&strlen(tokens[8])<=12) {
    Pixel::Binding b{};b.type=tokens[4][0];b.code=v;b.modifiers=m;b.color=color;strcpy(b.label,tokens[8]);
    if(!Pixel::valid(b)){error("BINDING");return;}config.bindings[p][k]=b;
    if(p==profile)dirtyTiles|=1<<k;ok("SET");return;
  }
  if(cmd=="RGB"&&n==3&&Pixel::number(tokens[2],80,v)){config.brightness=v;dirtyTiles=255;ok("RGB");return;}
  if(cmd=="SAVE"&&n==2){
    if(prefs.putBytes("config",&config,sizeof(config))==sizeof(config))ok("SAVED");else error("STORAGE");return;
  }
  if(cmd=="SDINFO"&&n==2){ok(sdReady?"READY|"+String(uint32_t(SD.cardSize()/1024/1024)):"ABSENT");return;}
  error("COMMAND");
}
void setup() {
  USB.productName("PIXEL PRO 2.0");USB.manufacturerName("PIXEL PRO");
  keyboard.begin();media.begin();usbLink.begin();USB.begin();
  // Enumeration must never wait for the app or an open serial port.
  prefs.begin("pixelpro2",false);loadConfig();
  for(auto p:Pins::rows)pinMode(p,INPUT);
  for(auto p:Pins::cols)pinMode(p,INPUT_PULLUP);
  pinMode(Pins::encoderA,INPUT_PULLUP);pinMode(Pins::encoderB,INPUT_PULLUP);pinMode(Pins::encoderPush,INPUT_PULLUP);
  analogReadResolution(10);
  leds.begin();leds.clear();leds.show();panel.begin();
  SPI.begin(Pins::sdSck,Pins::sdMiso,Pins::sdMosi,Pins::sdCs);
  sdReady=SD.begin(Pins::sdCs,SPI,20000000);
}
void loop() {
  uint32_t now=millis();scanKeys(now);
  if(consumerHeld&&int32_t(now-consumerUntil)>=0){media.release();consumerHeld=0;}
  // Bounded work: a stream of serial data cannot starve physical inputs.
  for(int count=0;count<192&&usbLink.available();++count) {
    int status=input.feed(char(usbLink.read()));
    if(status==1)request(input.text);else if(status<0)emit("R|0|ERR|FRAME");
  }
  scanTouch(now);render();delay(1);
}
