#include <Arduino.h>
#include <USB.h>
#include <USBCDC.h>
#include <USBHIDKeyboard.h>
#include <USBHIDConsumerControl.h>
#include <Preferences.h>
#include <SPI.h>
#include <SD.h>
#include <SPIFFS.h>
#include <FS.h>
#include <Adafruit_NeoPixel.h>
#include "esp_heap_caps.h"
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
struct TouchCalibration {
  uint32_t magic;
  int16_t left,right,top,bottom;
};
struct UploadState {
  bool active=false;
  bool icon=false;
  uint8_t iconProfile=0,iconKey=0;
  uint32_t expected=0,received=0,crcExpected=0,crc=0xFFFFFFFF;
  uint16_t fill=0;
  uint8_t buffer[512];
  File file;
};

Configuration config{};
TouchCalibration touchCal{0x50544331,942,139,136,907};
UploadState upload;
uint8_t profile=0;
uint8_t displayMode=0;
uint16_t saverSeconds=30;
Pixel::Debounce keys[8],push,touch;
bool suppressed[8]{};
Pixel::Binding held[8]{};
Pixel::LineBuffer<192> input;
bool displayDirty=true,sdReady=false,sdAttempted=false,flashReady=false,flashAttempted=false;
uint8_t dirtyTiles=255;
uint32_t lastScan=0,lastTouch=0,lastInput=0;
uint16_t consumerHeld=0;
uint32_t consumerUntil=0;

// Touch calibration
bool calibrating=false;
uint8_t calPoint=0;
int calRawX[4]{},calRawY[4]{};

// Media player
File mediaFile;
uint8_t* mediaFrame=nullptr;
uint8_t iconFrame[48*48]{};
uint16_t mediaW=0,mediaH=0,mediaFrames=0,mediaDelay=100,mediaIndex=0;
bool mediaActive=false;
uint32_t mediaNext=0;

// Full-screen PC monitor
bool monitorActive=false;
uint8_t monitorCpu=0,monitorGpu=0,monitorRam=0,monitorDisk=0;
uint8_t monitorCpuTemp=0,monitorGpuTemp=0;
uint16_t monitorNet=0;

void defaults() {
  memset(&config,0,sizeof(config));
  config.magic=0x50583201;
  config.brightness=24;
  for(int p=0;p<Pixel::Profiles;++p)for(int k=0;k<Pixel::Keys;++k) {
    auto& b=config.bindings[p][k];
    b.type='K';b.code=4+k;b.color=0x04BF;
    snprintf(b.label,sizeof(b.label),"Key %c",'A'+k);
  }
}

void loadConfig() {
  defaults();
  Configuration saved{};
  if(prefs.getBytesLength("config")==sizeof(saved)) {
    prefs.getBytes("config",&saved,sizeof(saved));
    bool valid=saved.magic==config.magic&&saved.brightness<=80;
    if(valid)for(auto& page:saved.bindings)for(auto& b:page)if(!Pixel::valid(b))valid=false;
    if(valid)config=saved;
  }
  TouchCalibration tc{};
  if(prefs.getBytesLength("touchcal")==sizeof(tc)) {
    prefs.getBytes("touchcal",&tc,sizeof(tc));
    if(tc.magic==touchCal.magic&&abs(tc.left-tc.right)>250&&abs(tc.top-tc.bottom)>250)
      touchCal=tc;
  }
  saverSeconds=prefs.getUShort("saver",30);
  if(saverSeconds>3600)saverSeconds=30;
}

void emit(const String& text) {
  // Do not gate replies on USBCDC::operator bool(). Arduino-ESP32's wrapper
  // requires its own DTR+RTS state to become "connected", while TinyUSB can
  // already have a valid CDC transport. USBCDC::write() safely returns 0 when
  // the actual CDC endpoint is unavailable.
  usbLink.println(text);
}

uint32_t crcUpdate(uint32_t crc,uint8_t b) {
  crc^=b;
  for(int i=0;i<8;i++)crc=(crc&1)?(crc>>1)^0xEDB88320UL:crc>>1;
  return crc;
}

void reportKeys() {
  KeyReport report{};
  int count=0;
  for(int i=0;i<8;++i)if(keys[i].stable&&!suppressed[i]&&held[i].type=='K') {
    report.modifiers|=held[i].modifiers;
    uint8_t code=held[i].code;
    bool duplicate=false;
    for(int j=0;j<count;++j)if(report.keys[j]==code)duplicate=true;
    if(!duplicate&&count<6)report.keys[count++]=code;
  }
  keyboard.sendReport(&report);
}

void stopSaver() {
  if(mediaFile)mediaFile.close();
  if(mediaActive) {
    mediaActive=false;
    displayDirty=true;
    dirtyTiles=255;
  }
}

void userActivity() {
  lastInput=millis();
  if(mediaActive)stopSaver();
}

void selectProfile(int p) {
  if(p<0||p>=Pixel::Profiles)return;
  profile=p;
  for(int k=0;k<8;++k)suppressed[k]=keys[k].stable;
  keyboard.releaseAll();
  media.release();
  consumerHeld=0;
  displayDirty=true;
  dirtyTiles=255;
  emit("E|PROFILE|"+String(profile));
}

void consumer(uint16_t code) {
  media.press(code);
  consumerHeld=code;
  consumerUntil=millis()+35;
}

void activate(const Pixel::Binding& b,int key,bool down) {
  if(!down)return;
  if(b.type=='C')consumer(b.code);
  if(b.type=='P')selectProfile(b.code);
  if(b.type=='H')emit("E|HOST|"+String(profile)+"|"+String(key));
}

void scanKeys(uint32_t now) {
  if(uint32_t(now-lastScan)<1)return;
  lastScan=now;
  bool changed=false;
  for(int r=0;r<2;++r) {
    digitalWrite(Pins::rows[r],LOW);
    pinMode(Pins::rows[r],OUTPUT);
    delayMicroseconds(5);
    for(int c=0;c<4;++c) {
      int k=r*4+c;
      if(keys[k].update(digitalRead(Pins::cols[c])==LOW,now)) {
        changed=true;
        dirtyTiles|=1<<k;
        userActivity();
        if(keys[k].stable){held[k]=config.bindings[profile][k];activate(held[k],k,true);}
        else suppressed[k]=false;
        emit("E|KEY|"+String(k)+"|"+String(keys[k].stable?1:0));
      }
    }
    pinMode(Pins::rows[r],INPUT);
  }
  if(changed)reportKeys();

  if(push.update(digitalRead(Pins::encoderPush)==LOW,now)&&push.stable) {
    userActivity();
    consumer(0xE2);
  }
  static uint8_t previous=3;
  static int8_t accumulator=0;
  static const int8_t transitions[16]={0,-1,1,0,1,0,0,-1,-1,0,0,1,0,1,-1,0};
  uint8_t state=(digitalRead(Pins::encoderA)<<1)|digitalRead(Pins::encoderB);
  accumulator+=transitions[(previous<<2)|state];
  previous=state;
  if(accumulator>=4){userActivity();consumer(0xE9);accumulator=0;}
  if(accumulator<=-4){userActivity();consumer(0xEA);accumulator=0;}
}

bool rawTouch(int& rawX,int& rawY,int& pressure) {
  constexpr int xp=39,xm=14,yp=13,ym=40;
  auto median5=[](int pin) {
    int v[5];
    for(int i=0;i<5;i++)v[i]=analogRead(pin);
    for(int i=1;i<5;i++) {
      int x=v[i],j=i-1;
      while(j>=0&&v[j]>x){v[j+1]=v[j];j--;}
      v[j+1]=x;
    }
    return v[2];
  };

  digitalWrite(Pins::cs,HIGH);

  pinMode(yp,INPUT);pinMode(ym,INPUT);
  pinMode(xp,OUTPUT);digitalWrite(xp,HIGH);
  pinMode(xm,OUTPUT);digitalWrite(xm,LOW);
  delayMicroseconds(15);
  int rx=1023-median5(yp);

  pinMode(xp,INPUT);pinMode(xm,INPUT);
  pinMode(yp,OUTPUT);digitalWrite(yp,HIGH);
  pinMode(ym,OUTPUT);digitalWrite(ym,LOW);
  delayMicroseconds(15);
  int ry=1023-median5(xm);

  pinMode(xp,OUTPUT);digitalWrite(xp,LOW);
  pinMode(ym,OUTPUT);digitalWrite(ym,HIGH);
  pinMode(xm,INPUT);pinMode(yp,INPUT);
  delayMicroseconds(15);
  int z1=median5(xm),z2=median5(yp);

  panel.restore();

  // Standard 4-wire resistive-touch pressure score: firmer presses approach
  // 1023. The previous resistance threshold rejected many normal/firm taps.
  pressure=constrain(1023-abs(z2-z1),0,1023);
  rawX=ry;
  rawY=rx;
  return pressure>=45&&rawX>=20&&rawX<=1003&&rawY>=20&&rawY<=1003;
}

void drawCalibrationTarget() {
  static const int tx[4]={24,455,455,24};
  static const int ty[4]={24,24,295,295};
  panel.fillScreen(0x0000);
  panel.setTextColor(0xFFFF);
  panel.setTextSize(2);
  panel.setCursor(145,145);
  panel.print("TOUCH ");
  panel.print(calPoint+1);
  panel.print("/4");
  int x=tx[calPoint],y=ty[calPoint];
  panel.drawLine(x-12,y,x+12,y,0xFFFF);
  panel.drawLine(x,y-12,x,y+12,0xFFFF);
  panel.drawCircle(x,y,7,0xF800);
}

void finishCalibration() {
  TouchCalibration next{
    touchCal.magic,
    int16_t((calRawX[0]+calRawX[3])/2),
    int16_t((calRawX[1]+calRawX[2])/2),
    int16_t((calRawY[0]+calRawY[1])/2),
    int16_t((calRawY[2]+calRawY[3])/2)
  };
  calibrating=false;
  panel.orientation(displayMode);
  if(abs(next.left-next.right)>250&&abs(next.top-next.bottom)>250&&
     prefs.putBytes("touchcal",&next,sizeof(next))==sizeof(next)) {
    touchCal=next;
    emit("E|CALDONE|"+String(next.left)+"|"+String(next.right)+"|"+
         String(next.top)+"|"+String(next.bottom));
  } else emit("E|CALFAIL|RANGE");
  displayDirty=true;
  dirtyTiles=255;
  userActivity();
}

void scanTouch(uint32_t now) {
  if(uint32_t(now-lastTouch)<8)return;
  lastTouch=now;
  int rx=0,ry=0,pressure=0;
  bool down=rawTouch(rx,ry,pressure);
  if(touch.update(down,now)&&touch.stable) {
    userActivity();
    if(calibrating) {
      calRawX[calPoint]=rx;
      calRawY[calPoint]=ry;
      calPoint++;
      if(calPoint>=4)finishCalibration();
      else drawCalibrationTarget();
      return;
    }
    // Calibration crosshairs are at 24..455 / 24..295, not at the panel
    // edges. Map those exact target coordinates and allow linear extrapolation
    // to the physical edges before constraining.
    int x=constrain(map(rx,touchCal.left,touchCal.right,24,455),0,479);
    int y=constrain(map(ry,touchCal.top,touchCal.bottom,24,295),0,319);
    Pixel::orientTouch(displayMode,x,y);
    if(y>=280)selectProfile(constrain(x/96,0,4));
    emit("E|TOUCH|"+String(x)+"|"+String(y)+"|"+String(pressure));
  }
}

bool ensureFlash(bool allowFormat) {
  if(flashReady)return true;
  if(!flashAttempted) {
    flashAttempted=true;
    flashReady=SPIFFS.begin(false);
  }
  if(!flashReady&&allowFormat) {
    SPIFFS.end();
    flashReady=SPIFFS.begin(true);
  }
  return flashReady;
}

bool ensureSd() {
  if(sdReady)return true;
  if(sdAttempted)return false;
  sdAttempted=true;
  SPI.begin(Pins::sdSck,Pins::sdMiso,Pins::sdMosi,Pins::sdCs);
  sdReady=SD.begin(Pins::sdCs,SPI,20000000);
  return sdReady;
}

bool readIconHeader(File& file,uint16_t& w,uint16_t& h) {
  uint8_t header[8]{};
  if(!file||file.read(header,sizeof(header))!=sizeof(header))return false;
  if(memcmp(header,"PXI1",4)!=0)return false;
  w=uint16_t(header[4]|(uint16_t(header[5])<<8));
  h=uint16_t(header[6]|(uint16_t(header[7])<<8));
  if(w!=48||h!=48)return false;
  return file.size()==8UL+uint32_t(w)*h;
}

bool drawKeyIcon(uint8_t p,uint8_t k,int16_t x,int16_t y) {
  if(!flashReady)return false;
  char path[16];
  snprintf(path,sizeof(path),"/i%u%u.pxi",unsigned(p),unsigned(k));
  File file=SPIFFS.open(path,FILE_READ);
  uint16_t w=0,h=0;
  if(!readIconHeader(file,w,h)){if(file)file.close();return false;}
  uint32_t bytes=uint32_t(w)*h;
  bool ok=file.read(iconFrame,bytes)==bytes;
  file.close();
  if(!ok)return false;
  panel.drawRgb332(x,y,iconFrame,w,h);
  return true;
}

void drawMonitorMetric(const char* name,uint8_t value,uint8_t temp,int y,uint16_t color) {
  panel.setTextColor(0xFFFF);
  panel.setTextSize(2);
  panel.setCursor(24,y);
  panel.print(name);
  panel.setCursor(340,y);
  panel.print(value);
  panel.print("%");
  panel.setTextSize(1);
  panel.setCursor(410,y+5);
  if(temp){panel.print(temp);panel.print("C");}
  else panel.print("--C");
  panel.drawRect(120,y+2,205,18,0x7BEF);
  panel.fillRect(122,y+4,201,14,0x1082);
  int w=int(value)*197/100;
  if(w>0)panel.fillRect(124,y+5,w,12,color);
}

void drawMonitorScreen() {
  panel.fillScreen(0x0843);
  panel.setTextColor(0xFFFF);
  panel.setTextSize(3);
  panel.setCursor(22,18);
  panel.print("PC MONITOR");
  panel.setTextSize(1);
  panel.setCursor(365,28);
  panel.print("PIXEL PRO 2.0");
  drawMonitorMetric("CPU",monitorCpu,monitorCpuTemp,72,0x07E0);
  drawMonitorMetric("GPU",monitorGpu,monitorGpuTemp,116,0xF81F);
  drawMonitorMetric("RAM",monitorRam,0,160,0x07FF);
  drawMonitorMetric("DISK",monitorDisk,0,204,0xFFE0);
  panel.setTextSize(2);
  panel.setTextColor(0xFFFF);
  panel.setCursor(24,254);
  panel.print("NET");
  panel.setCursor(120,254);
  panel.print(monitorNet);
  panel.print(" kbps");
  panel.setTextSize(1);
  panel.setCursor(24,295);
  panel.print("Keys/HID remain active  |  PC Monitor from Studio");
}

void render() {
  if(mediaActive||calibrating||monitorActive)return;
  if(displayDirty) {
    panel.fillScreen(0x0843);
    panel.setTextSize(2);
    panel.setTextColor(0xFFFF);
    panel.setCursor(12,10);
    panel.print("PIXEL PRO 2.0");
    for(int p=0;p<5;++p) {
      panel.fillRect(p*96+2,282,92,36,p==profile?0x04BF:0x18C6);
      panel.setCursor(p*96+13,292);
      panel.print("P");
      panel.print(p+1);
    }
    displayDirty=false;
  }
  for(int k=0;k<8;++k)if(dirtyTiles&(1<<k)) {
    dirtyTiles&=~(1<<k);
    auto& b=config.bindings[profile][k];
    int x=(k%4)*120+4,y=(k/4)*112+48;
    panel.fillRect(x,y,112,104,keys[k].stable?0xFFFF:0x18C6);
    panel.fillRect(x,y,112,4,b.color);
    panel.setTextColor(keys[k].stable?0x0843:0xFFFF);
    panel.setTextSize(1);
    panel.setCursor(x+7,y+8);
    panel.print(k+1);
    bool hasIcon=drawKeyIcon(profile,k,x+32,y+10);
    if(!hasIcon) {
      panel.setTextSize(2);
      panel.setCursor(x+43,y+30);
      panel.print(k+1);
    }
    panel.setTextSize(1);
    panel.setCursor(x+8,y+70);
    panel.print(b.label);
    static const uint8_t order[]={0,1,2,3,7,6,5,4};
    leds.setPixelColor(order[k],leds.Color(((b.color>>11)&31)*8,((b.color>>5)&63)*4,(b.color&31)*8));
    leds.setBrightness(config.brightness);
    leds.show();
    break;
  }
}

bool readMediaHeader(File& file) {
  uint8_t h[12]{};
  if(!file||file.read(h,sizeof(h))!=sizeof(h))return false;
  if(memcmp(h,"PXG1",4)!=0)return false;
  auto u16=[&](int i){return uint16_t(h[i]|(uint16_t(h[i+1])<<8));};
  mediaW=u16(4);mediaH=u16(6);mediaFrames=u16(8);mediaDelay=u16(10);
  if(!mediaW||!mediaH||mediaW>160||mediaH>106||!mediaFrames||mediaDelay<40||mediaDelay>2000)return false;
  uint32_t expected=12UL+uint32_t(mediaW)*mediaH*mediaFrames;
  return file.size()==expected;
}

void startSaver(uint32_t now) {
  if(!flashReady||!mediaFrame||calibrating||upload.active||mediaActive)return;
  mediaFile=SPIFFS.open("/screensaver.pxg",FILE_READ);
  if(!readMediaHeader(mediaFile)){if(mediaFile)mediaFile.close();return;}
  mediaIndex=0;
  mediaNext=now;
  mediaActive=true;
  panel.fillScreen(0x0000);
}

void playSaver(uint32_t now) {
  if(!mediaActive||int32_t(now-mediaNext)<0)return;
  uint32_t bytes=uint32_t(mediaW)*mediaH;
  if(mediaFile.read(mediaFrame,bytes)!=bytes){stopSaver();return;}
  panel.drawRgb332Scaled3(mediaFrame,mediaW,mediaH);
  mediaIndex++;
  if(mediaIndex>=mediaFrames) {
    mediaIndex=0;
    mediaFile.seek(12);
  }
  mediaNext=now+mediaDelay;
}

void abortUpload(const char* why) {
  if(upload.file)upload.file.close();
  if(flashReady)SPIFFS.remove("/upload.tmp");
  upload.active=false;
  upload.fill=0;
  emit("E|MEDIADONE|"+String(why));
}

void finishUpload() {
  if(upload.file)upload.file.close();
  uint32_t crc=upload.crc^0xFFFFFFFFUL;
  if(upload.received!=upload.expected||crc!=upload.crcExpected){abortUpload("CRC");return;}

  File check=SPIFFS.open("/upload.tmp",FILE_READ);
  bool valid=false;
  if(upload.icon) {
    uint16_t w=0,h=0;
    valid=readIconHeader(check,w,h);
  } else valid=readMediaHeader(check);
  if(check)check.close();
  if(!valid){abortUpload("FORMAT");return;}

  char target[24];
  if(upload.icon)snprintf(target,sizeof(target),"/i%u%u.pxi",unsigned(upload.iconProfile),unsigned(upload.iconKey));
  else strcpy(target,"/screensaver.pxg");
  SPIFFS.remove(target);
  if(!SPIFFS.rename("/upload.tmp",target)){abortUpload("RENAME");return;}

  if(upload.icon&&upload.iconProfile==profile)dirtyTiles|=1<<upload.iconKey;
  upload.active=false;
  upload.fill=0;
  userActivity();
  emit("E|MEDIADONE|OK");
}

void consumeUpload() {
  if(!upload.active)return;
  uint32_t remaining=upload.expected-upload.received;
  uint16_t needed=uint16_t(remaining>sizeof(upload.buffer)?sizeof(upload.buffer):remaining);
  while(upload.fill<needed&&usbLink.available()) {
    int c=usbLink.read();
    if(c<0)break;
    upload.buffer[upload.fill++]=uint8_t(c);
  }
  if(upload.fill==needed&&needed) {
    if(upload.file.write(upload.buffer,needed)!=needed){abortUpload("WRITE");return;}
    for(uint16_t i=0;i<needed;i++)upload.crc=crcUpdate(upload.crc,upload.buffer[i]);
    upload.received+=needed;
    upload.fill=0;
    emit("E|MEDIAACK|"+String(upload.received));
    if(upload.received>=upload.expected)finishUpload();
  }
}

void request(char* line) {
  char* tokens[12]{};
  int n=0;
  char* cursor=line;
  do {
    if(n==12){emit("R|0|ERR|FRAME");return;}
    tokens[n++]=cursor;
    cursor=strchr(cursor,'|');
    if(cursor)*cursor++=0;
  } while(cursor);

  uint32_t id=0;
  if(n<2||!Pixel::number(tokens[0],65535,id)){emit("R|0|ERR|ID");return;}
  String prefix="R|"+String(id)+"|";
  auto ok=[&](const String& s){emit(prefix+"OK|"+s);};
  auto error=[&](const char* s){emit(prefix+"ERR|"+s);};

  userActivity();
  String cmd=tokens[1];
  uint32_t p=0,k=0,v=0,m=0,color=0,a=0,b=0;

  if(cmd=="HELLO"&&n==2) {
    ok("PIXELPRO2|2.3.0|5|8|HX8357B|HID,CDC,RGB,TOUCH,TOUCHCAL,SD,PANEL,MEDIA,SAVER,ICON,MONITOR");
    return;
  }
  if(cmd=="PANEL"&&n==2){ok(String(displayMode));return;}
  if(cmd=="DISPLAY"&&n==3&&Pixel::number(tokens[2],3,v)) {
    if(prefs.putUChar("orient2",v)!=1){error("STORAGE");return;}
    displayMode=v;
    panel.orientation(displayMode);
    displayDirty=true;dirtyTiles=255;
    ok("DISPLAY");return;
  }
  if(cmd=="STATE"&&n==2) {
    ok(String(profile)+"|"+String(config.brightness)+"|"+String(saverSeconds));
    return;
  }
  if(cmd=="SAVER"&&n==3&&Pixel::number(tokens[2],3600,v)) {
    if(prefs.putUShort("saver",v)!=2){error("STORAGE");return;}
    saverSeconds=v;
    ok("SAVER");return;
  }
  if(cmd=="MONITOR"&&n==3&&strcmp(tokens[2],"OFF")==0) {
    monitorActive=false;
    displayDirty=true;
    dirtyTiles=255;
    ok("OFF");
    return;
  }
  if(cmd=="MONITOR"&&n==10&&strcmp(tokens[2],"SET")==0&&
     Pixel::number(tokens[3],100,p)&&Pixel::number(tokens[4],100,k)&&
     Pixel::number(tokens[5],100,v)&&Pixel::number(tokens[6],100,m)&&
     Pixel::number(tokens[7],9999,color)&&Pixel::number(tokens[8],125,a)&&
     Pixel::number(tokens[9],125,b)) {
    if(calibrating||upload.active){error("BUSY");return;}
    stopSaver();
    monitorCpu=p;monitorGpu=k;monitorRam=v;monitorDisk=m;monitorNet=color;
    monitorCpuTemp=a;monitorGpuTemp=b;
    monitorActive=true;
    drawMonitorScreen();
    ok("MONITOR");
    return;
  }
  if(cmd=="PROFILE"&&n==3&&Pixel::number(tokens[2],4,p)) {
    selectProfile(p);ok("PROFILE");return;
  }
  if(cmd=="GET"&&n==4&&Pixel::number(tokens[2],4,p)&&Pixel::number(tokens[3],7,k)) {
    auto& b=config.bindings[p][k];
    ok(String(b.type)+"|"+String(b.code)+"|"+String(b.modifiers)+"|"+String(b.color)+"|"+b.label);
    return;
  }
  if(cmd=="SET"&&n==9&&Pixel::number(tokens[2],4,p)&&Pixel::number(tokens[3],7,k)&&
     strlen(tokens[4])==1&&Pixel::number(tokens[5],65535,v)&&Pixel::number(tokens[6],255,m)&&
     Pixel::number(tokens[7],65535,color)&&strlen(tokens[8])<=12) {
    Pixel::Binding b{};
    b.type=tokens[4][0];b.code=v;b.modifiers=m;b.color=color;
    strcpy(b.label,tokens[8]);
    if(!Pixel::valid(b)){error("BINDING");return;}
    config.bindings[p][k]=b;
    if(p==profile)dirtyTiles|=1<<k;
    ok("SET");return;
  }
  if(cmd=="RGB"&&n==3&&Pixel::number(tokens[2],80,v)) {
    config.brightness=v;dirtyTiles=255;ok("RGB");return;
  }
  if(cmd=="SAVE"&&n==2) {
    if(prefs.putBytes("config",&config,sizeof(config))==sizeof(config))ok("SAVED");
    else error("STORAGE");
    return;
  }
  if(cmd=="SDINFO"&&n==2) {
    bool ready=ensureSd();
    ok(ready?"READY|"+String(uint32_t(SD.cardSize()/1024/1024)):"ABSENT");
    return;
  }
  if(cmd=="TOUCHCAL"&&n==3&&strcmp(tokens[2],"START")==0) {
    monitorActive=false;
    stopSaver();
    calibrating=true;
    calPoint=0;
    panel.orientation(0);
    drawCalibrationTarget();
    ok("STARTED");
    return;
  }
  if(cmd=="TOUCHCAL"&&n==3&&strcmp(tokens[2],"GET")==0) {
    ok(String(touchCal.left)+"|"+String(touchCal.right)+"|"+
       String(touchCal.top)+"|"+String(touchCal.bottom));
    return;
  }
  if(cmd=="MEDIA"&&n==3&&strcmp(tokens[2],"INFO")==0) {
    if(!ensureFlash(false)){ok("ABSENT");return;}
    File f=SPIFFS.open("/screensaver.pxg",FILE_READ);
    if(!f){ok("ABSENT");return;}
    bool valid=readMediaHeader(f);
    uint32_t size=f.size();
    if(f)f.close();
    if(valid)ok("READY|"+String(size)+"|"+String(mediaW)+"|"+String(mediaH)+"|"+
                String(mediaFrames)+"|"+String(mediaDelay));
    else ok("INVALID");
    return;
  }
  if(cmd=="MEDIA"&&n==3&&strcmp(tokens[2],"DELETE")==0) {
    stopSaver();
    if(!ensureFlash(false)){ok("DELETED");return;}
    SPIFFS.remove("/screensaver.pxg");
    SPIFFS.remove("/upload.tmp");
    ok("DELETED");return;
  }
  if(cmd=="ICON"&&n==5&&strcmp(tokens[2],"DELETE")==0&&
     Pixel::number(tokens[3],4,p)&&Pixel::number(tokens[4],7,k)) {
    if(!ensureFlash(false)){ok("DELETED");return;}
    char path[16];
    snprintf(path,sizeof(path),"/i%u%u.pxi",unsigned(p),unsigned(k));
    SPIFFS.remove(path);
    if(p==profile)dirtyTiles|=1<<k;
    ok("DELETED");return;
  }
  if(cmd=="ICON"&&n==7&&strcmp(tokens[2],"BEGIN")==0&&
     Pixel::number(tokens[3],4,p)&&Pixel::number(tokens[4],7,k)&&
     Pixel::number(tokens[5],8192,v)&&Pixel::number(tokens[6],0xFFFFFFFFUL,m)) {
    if(!ensureFlash(true)||v<8){error("ICON");return;}
    stopSaver();
    if(upload.file)upload.file.close();
    SPIFFS.remove("/upload.tmp");
    upload.file=SPIFFS.open("/upload.tmp",FILE_WRITE);
    if(!upload.file){error("FLASH");return;}
    upload.active=true;
    upload.icon=true;
    upload.iconProfile=p;
    upload.iconKey=k;
    upload.expected=v;
    upload.received=0;
    upload.crcExpected=m;
    upload.crc=0xFFFFFFFFUL;
    upload.fill=0;
    ok("READY|512");
    return;
  }

  if(cmd=="MEDIA"&&n==5&&strcmp(tokens[2],"BEGIN")==0&&
     Pixel::number(tokens[3],1900000,v)&&Pixel::number(tokens[4],0xFFFFFFFFUL,m)) {
    if(!ensureFlash(true)||v<12){error("MEDIA");return;}
    stopSaver();
    if(upload.file)upload.file.close();
    SPIFFS.remove("/upload.tmp");
    upload.file=SPIFFS.open("/upload.tmp",FILE_WRITE);
    if(!upload.file){error("FLASH");return;}
    upload.active=true;
    upload.icon=false;
    upload.expected=v;
    upload.received=0;
    upload.crcExpected=m;
    upload.crc=0xFFFFFFFFUL;
    upload.fill=0;
    ok("READY|512");
    return;
  }
  error("COMMAND");
}

void setup() {
  USB.productName("PIXEL PRO 2.0");
  USB.manufacturerName("PIXEL PRO");
  keyboard.begin();
  media.begin();
  usbLink.enableReboot(false);
  usbLink.setTxTimeoutMs(25);
  usbLink.begin();
  USB.begin();

  prefs.begin("pixelpro2",false);
  loadConfig();
  // orient2 deliberately ignores the old orientation value: that mapping used
  // the wrong physical origin for this panel revision.
  displayMode=prefs.getUChar("orient2",0);
  if(displayMode>3)displayMode=0;

  for(auto p:Pins::rows)pinMode(p,INPUT);
  for(auto p:Pins::cols)pinMode(p,INPUT_PULLUP);
  pinMode(Pins::encoderA,INPUT_PULLUP);
  pinMode(Pins::encoderB,INPUT_PULLUP);
  pinMode(Pins::encoderPush,INPUT_PULLUP);
  analogReadResolution(10);

  leds.begin();
  leds.clear();
  leds.show();
  panel.begin(displayMode);

  // Draw the key UI before touching optional filesystems so the device feels
  // ready immediately after reset/flash.
  render();

  // Mount existing SPIFFS without formatting. A blank full-flash image used to
  // trigger an expensive format here and made first boot look frozen. Formatting
  // is now deferred until the first icon/GIF upload.
  flashAttempted=true;
  flashReady=SPIFFS.begin(false);
  if(flashReady)dirtyTiles=255;

  mediaFrame=(uint8_t*)heap_caps_malloc(160UL*106UL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
  if(!mediaFrame)mediaFrame=(uint8_t*)malloc(160UL*106UL);

  // SD is optional and probed only when Studio asks for SDINFO.
  lastInput=millis();
}

void loop() {
  uint32_t now=millis();
  scanKeys(now);
  if(consumerHeld&&int32_t(now-consumerUntil)>=0){media.release();consumerHeld=0;}

  if(upload.active) {
    consumeUpload();
    delay(1);
    return;
  }

  for(int count=0;count<192&&usbLink.available();++count) {
    int status=input.feed(char(usbLink.read()));
    if(status==1)request(input.text);
    else if(status<0)emit("R|0|ERR|FRAME");
  }

  scanTouch(now);

  if(!calibrating&&!mediaActive&&saverSeconds>0&&uint32_t(now-lastInput)>=uint32_t(saverSeconds)*1000UL)
    startSaver(now);
  if(mediaActive)playSaver(now);
  else render();

  delay(1);
}
