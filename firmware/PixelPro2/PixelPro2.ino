#include <Arduino.h>
#include <math.h>
#include <USB.h>
#include <USBCDC.h>
#include <USBHIDKeyboard.h>
#include <USBHIDConsumerControl.h>
#include <USBHIDMouse.h>
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
USBHIDMouse mouse;
Preferences prefs;
Panel panel;
Adafruit_NeoPixel leds(8,Pins::rgb,NEO_GRB+NEO_KHZ800);

struct Configuration {
  uint32_t magic;
  Pixel::Binding bindings[Pixel::Profiles][Pixel::Keys];
  uint8_t brightness;
};
struct LegacyConfiguration5 {
  uint32_t magic;
  Pixel::Binding bindings[5][Pixel::Keys];
  uint8_t brightness;
};
struct TouchCalibration {
  uint32_t magic;
  float ax,bx,cx;
  float ay,by,cy;
};
struct UploadState {
  bool active=false;
  bool icon=false;
  bool script=false;
  uint8_t iconProfile=0,iconKey=0;
  uint8_t scriptProfile=0,scriptKey=0;
  uint32_t expected=0,received=0,crcExpected=0,crc=0xFFFFFFFF;
  uint16_t fill=0;
  uint8_t buffer[512];
  File file;
};

Configuration config{};
TouchCalibration touchCal{0x50544332,0,0,0,0,0,0};
bool touchCalValid=false;
UploadState upload;
uint8_t profile=0;
uint8_t displayMode=0;
uint16_t saverSeconds=30,screenOffSeconds=0;
bool panelAwake=true;
Pixel::Debounce keys[8],push;
bool suppressed[8]{};
Pixel::Binding held[8]{};
Pixel::LineBuffer<192> input;
bool displayDirty=true,sdReady=false,sdAttempted=false,flashReady=false,flashAttempted=false;
uint8_t dirtyTiles=255;
uint32_t lastScan=0,lastTouch=0,lastInput=0;
uint16_t consumerHeld=0;
uint32_t consumerUntil=0;

// Touch calibration / filtering. Raw axes follow TouchScreen.h:
// rawX is sampled on YP(D13); rawY is sampled on XM(D14).
bool calibrating=false,touchStablePressed=false,touchDiag=false;
uint8_t calPoint=0,touchPressConfirmations=0,touchReleaseMisses=0,calSampleCount=0;
uint16_t touchCandidateX=0,touchCandidateY=0;
uint16_t calRawX[4]{},calRawY[4]{};
uint16_t calSamplesX[7]{},calSamplesY[7]{};
uint32_t touchPressStartedAt=0,lastTouchDiag=0;

// Media player
File mediaFile;
uint8_t* mediaFrame=nullptr;
uint8_t iconFrame[48*48]{};
uint16_t mediaW=0,mediaH=0,mediaFrames=0,mediaDelay=100,mediaIndex=0;
bool mediaActive=false;
uint32_t mediaNext=0;

// Native HID script (PXS2), loaded on demand from SPIFFS.
uint8_t scriptData[8192]{};
uint16_t scriptSize=0,scriptPos=0,scriptSteps=0;
bool scriptActive=false;
uint32_t scriptResumeAt=0;

// Full-screen PC monitor
bool monitorActive=false;
uint8_t monitorCpu=0,monitorGpu=0,monitorRam=0,monitorDisk=0;
uint8_t monitorCpuTemp=0,monitorGpuTemp=0;
uint16_t monitorNet=0;

// Integrated SMTC music plugin
bool musicActive=false,musicPlaying=false;
char musicTitle[49]{},musicArtist[49]{},musicAlbum[49]{};

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
  size_t configBytes=prefs.getBytesLength("config");
  if(configBytes==sizeof(saved)) {
    prefs.getBytes("config",&saved,sizeof(saved));
    bool valid=saved.magic==config.magic&&saved.brightness<=80;
    if(valid)for(auto& page:saved.bindings)for(auto& b:page)if(!Pixel::valid(b))valid=false;
    if(valid)config=saved;
  } else if(configBytes==sizeof(LegacyConfiguration5)) {
    LegacyConfiguration5 legacy{};
    prefs.getBytes("config",&legacy,sizeof(legacy));
    bool valid=legacy.magic==config.magic&&legacy.brightness<=80;
    if(valid)for(auto& page:legacy.bindings)for(auto& b:page)if(!Pixel::valid(b))valid=false;
    if(valid) {
      config.brightness=legacy.brightness;
      for(int p=0;p<5;p++)for(int k=0;k<Pixel::Keys;k++)
        config.bindings[p][k]=legacy.bindings[p][k];
    }
  }
  TouchCalibration tc{};
  if(prefs.getBytesLength("touchcal2")==sizeof(tc)) {
    prefs.getBytes("touchcal2",&tc,sizeof(tc));
    const float values[6]={tc.ax,tc.bx,tc.cx,tc.ay,tc.by,tc.cy};
    bool valid=tc.magic==touchCal.magic;
    for(float value:values)valid=valid&&isfinite(value);
    valid=valid&&(fabsf(tc.ax)+fabsf(tc.bx)>0.02f)&&
                (fabsf(tc.ay)+fabsf(tc.by)>0.02f)&&
                fabsf(tc.ax)<10.0f&&fabsf(tc.bx)<10.0f&&
                fabsf(tc.ay)<10.0f&&fabsf(tc.by)<10.0f&&
                fabsf(tc.cx)<10000.0f&&fabsf(tc.cy)<10000.0f;
    if(valid){touchCal=tc;touchCalValid=true;}
  }
  saverSeconds=prefs.getUShort("saver",30);
  if(saverSeconds>3600)saverSeconds=30;
  screenOffSeconds=prefs.getUShort("screenoff",0);
  if(screenOffSeconds!=0&&screenOffSeconds!=30&&screenOffSeconds!=300&&screenOffSeconds!=900)
    screenOffSeconds=0;
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

bool ensureFlash(bool allowFormat);
void selectProfile(int p);
void consumer(uint16_t code);

bool validMediaCode(uint16_t code) {
  return code==0xE9||code==0xEA||code==0xE2||code==0xCD||
         code==0xB5||code==0xB6||code==0xB7;
}

bool validateScriptBytes(const uint8_t* d,size_t n) {
  if(!d||n<7||n>sizeof(scriptData)||memcmp(d,"PXS2",4)!=0)return false;
  uint16_t steps=uint16_t(d[4]|(uint16_t(d[5])<<8));
  if(!steps||steps>512)return false;
  size_t p=6;
  for(uint16_t s=0;s<steps;s++) {
    if(p>=n)return false;
    uint8_t type=d[p++];
    if(type==1) {
      if(p>=n)return false;
      uint8_t len=d[p++];
      if(!len||len>96||p+len>n)return false;
      for(uint8_t i=0;i<len;i++)if(!(d[p+i]==9||d[p+i]==10||(d[p+i]>=32&&d[p+i]<=126)))return false;
      p+=len;
    } else if(type==2) {
      if(p+2>n)return false;
      p++; // modifiers
      uint8_t count=d[p++];
      if(!count||count>6||p+count>n)return false;
      for(uint8_t i=0;i<count;i++)if(d[p+i]<4||d[p+i]>115)return false;
      p+=count;
    } else if(type==3) {
      if(p+2>n)return false;
      p+=2;
    } else if(type==4) {
      if(p>=n||d[p]<1||d[p]>4)return false;
      p++;
    } else if(type==5) {
      if(p>=n||int8_t(d[p])==0)return false;
      p++;
    } else if(type==6) {
      if(p+2>n)return false;
      uint16_t code=uint16_t(d[p]|(uint16_t(d[p+1])<<8));
      if(!validMediaCode(code))return false;
      p+=2;
    } else if(type==7) {
      if(p>=n||d[p]>=Pixel::Profiles)return false;
      p++;
    } else if(type==8) {
      if(p>=n||d[p]<1||d[p]>2)return false;
      p++;
    } else if(type==9) {
      if(p+2>n)return false;
      p+=2;
    } else if(type==12) {
      if(p+3>n)return false;
      uint16_t delayMs=uint16_t(d[p]|(uint16_t(d[p+1])<<8));
      p+=2;
      if(delayMs>30000)return false;
      uint8_t len=d[p++];
      if(!len||p+len>n)return false;
      for(uint8_t i=0;i<len;i++)if(d[p+i]<32||d[p+i]>126)return false;
      p+=len;
    } else if(type==13) {
      if(p+4>n)return false;
      uint16_t duration=uint16_t(d[p]|(uint16_t(d[p+1])<<8));
      p+=2;
      if(duration>30000)return false;
      p++; // modifiers
      uint8_t count=d[p++];
      if(!count||count>6||p+count>n)return false;
      for(uint8_t i=0;i<count;i++)if(d[p+i]<4||d[p+i]>115)return false;
      p+=count;
    } else return false;
  }
  return p==n;
}

bool readScriptFile(uint8_t p,uint8_t k) {
  if(!ensureFlash(false))return false;
  char path[16];
  snprintf(path,sizeof(path),"/s%u%u.pxs",unsigned(p),unsigned(k));
  File file=SPIFFS.open(path,FILE_READ);
  if(!file||file.size()>sizeof(scriptData)||file.size()<7){if(file)file.close();return false;}
  scriptSize=file.size();
  bool ok=file.read(scriptData,scriptSize)==scriptSize;
  file.close();
  return ok&&validateScriptBytes(scriptData,scriptSize);
}

void startScript(uint8_t p,uint8_t k) {
  if(!readScriptFile(p,k)){emit("E|SCRIPTERR|MISSING");return;}
  scriptPos=6;
  scriptSteps=uint16_t(scriptData[4]|(uint16_t(scriptData[5])<<8));
  scriptActive=true;
  scriptResumeAt=0;
}

void runScript(uint32_t now) {
  if(!scriptActive||int32_t(now-scriptResumeAt)<0)return;
  for(int budget=0;budget<6&&scriptActive;budget++) {
    if(!scriptSteps||scriptPos>=scriptSize){scriptActive=false;reportKeys();return;}
    uint8_t type=scriptData[scriptPos++];
    scriptSteps--;

    if(type==1) {
      uint8_t len=scriptData[scriptPos++];
      for(uint8_t i=0;i<len;i++)keyboard.write(scriptData[scriptPos++]);
      reportKeys();
    } else if(type==2) {
      uint8_t modifiers=scriptData[scriptPos++];
      uint8_t count=scriptData[scriptPos++];
      KeyReport report{};
      report.modifiers=modifiers;
      for(uint8_t i=0;i<count;i++)report.keys[i]=scriptData[scriptPos++];
      keyboard.sendReport(&report);
      delay(12);
      KeyReport empty{};
      keyboard.sendReport(&empty);
      reportKeys();
    } else if(type==3) {
      uint16_t ms=uint16_t(scriptData[scriptPos]|(uint16_t(scriptData[scriptPos+1])<<8));
      scriptPos+=2;
      scriptResumeAt=now+ms;
      return;
    } else if(type==4) {
      uint8_t code=scriptData[scriptPos++];
      uint8_t button=code==1?MOUSE_LEFT:code==2?MOUSE_RIGHT:MOUSE_MIDDLE;
      mouse.click(button);
      if(code==4){delay(35);mouse.click(MOUSE_LEFT);}
    } else if(type==5) {
      int8_t wheel=int8_t(scriptData[scriptPos++]);
      mouse.move(0,0,wheel);
    } else if(type==6) {
      uint16_t code=uint16_t(scriptData[scriptPos]|(uint16_t(scriptData[scriptPos+1])<<8));
      scriptPos+=2;
      consumer(code);
    } else if(type==7) {
      selectProfile(scriptData[scriptPos++]);
    } else if(type==8) {
      uint8_t ctrl=scriptData[scriptPos++];
      selectProfile((profile+(ctrl==1?1:Pixel::Profiles-1))%Pixel::Profiles);
    } else if(type==9) {
      int8_t x=int8_t(scriptData[scriptPos++]);
      int8_t y=int8_t(scriptData[scriptPos++]);
      mouse.move(x,y,0);
    } else if(type==12) {
      uint16_t charDelay=uint16_t(scriptData[scriptPos]|(uint16_t(scriptData[scriptPos+1])<<8));
      scriptPos+=2;
      uint8_t len=scriptData[scriptPos++];
      for(uint8_t i=0;i<len;i++) {
        keyboard.write(scriptData[scriptPos++]);
        if(charDelay)delay(charDelay);
      }
      reportKeys();
    } else if(type==13) {
      uint16_t duration=uint16_t(scriptData[scriptPos]|(uint16_t(scriptData[scriptPos+1])<<8));
      scriptPos+=2;
      uint8_t modifiers=scriptData[scriptPos++];
      uint8_t count=scriptData[scriptPos++];
      KeyReport report{};
      report.modifiers=modifiers;
      for(uint8_t i=0;i<count;i++)report.keys[i]=scriptData[scriptPos++];
      keyboard.sendReport(&report);
      if(duration)delay(duration);
      KeyReport empty{};
      keyboard.sendReport(&empty);
      reportKeys();
    }
  }
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
  if(!panelAwake) {
    panel.displayOn();
    panelAwake=true;
    displayDirty=true;
    dirtyTiles=255;
  }
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
  if(b.type=='M') {
    if(b.code==1)mouse.click(MOUSE_LEFT);
    else if(b.code==2)mouse.click(MOUSE_RIGHT);
    else if(b.code==3)mouse.click(MOUSE_MIDDLE);
    else if(b.code==4){mouse.click(MOUSE_LEFT);delay(35);mouse.click(MOUSE_LEFT);}
    else if(b.code==5)mouse.move(0,0,1);
    else if(b.code==6)mouse.move(0,0,-1);
  }
  if(b.type=='P')selectProfile(b.code);
  if(b.type=='S')startScript(profile,key);
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

static uint16_t touchDelta(uint16_t a,uint16_t b) {
  return a>=b?a-b:b-a;
}

static uint16_t touchMedian3(uint16_t a,uint16_t b,uint16_t c) {
  if(a>b){uint16_t t=a;a=b;b=t;}
  if(b>c){uint16_t t=b;b=c;c=t;}
  if(a>b){uint16_t t=a;a=b;b=t;}
  return b;
}

static uint16_t touchMedian7(uint16_t* v) {
  for(int i=1;i<7;i++) {
    uint16_t x=v[i];int j=i-1;
    while(j>=0&&v[j]>x){v[j+1]=v[j];j--;}
    v[j+1]=x;
  }
  return v[3];
}

static void restoreTouchSharedPins() {
  digitalWrite(Pins::cs,HIGH);pinMode(Pins::cs,OUTPUT);
  pinMode(13,OUTPUT);digitalWrite(13,HIGH);
  pinMode(14,OUTPUT);digitalWrite(14,HIGH);
  pinMode(39,OUTPUT);digitalWrite(39,LOW);
  pinMode(40,OUTPUT);digitalWrite(40,LOW);
  delayMicroseconds(4);
}

static uint16_t readTouchAdc10(uint8_t pin) {
  (void)analogRead(pin);
  uint32_t sum=0;
  for(int i=0;i<3;i++){sum+=uint16_t(analogRead(pin));delayMicroseconds(8);}
  return uint16_t(sum/3U);
}

static void sampleTouchCoordinates(uint16_t& rawX,uint16_t& rawY) {
  constexpr uint8_t xp=39,xm=14,yp=13,ym=40;
  pinMode(Pins::cs,OUTPUT);digitalWrite(Pins::cs,HIGH);

  digitalWrite(yp,LOW);digitalWrite(ym,LOW);
  pinMode(yp,INPUT);pinMode(ym,INPUT);
  pinMode(xp,OUTPUT);pinMode(xm,OUTPUT);
  digitalWrite(xp,HIGH);digitalWrite(xm,LOW);
  delayMicroseconds(28);
  rawX=uint16_t(1023-readTouchAdc10(yp));

  digitalWrite(xp,LOW);digitalWrite(xm,LOW);
  pinMode(xp,INPUT);pinMode(xm,INPUT);
  pinMode(yp,OUTPUT);pinMode(ym,OUTPUT);
  digitalWrite(yp,HIGH);digitalWrite(ym,LOW);
  delayMicroseconds(28);
  rawY=uint16_t(1023-readTouchAdc10(xm));
}

static bool readTouchRaw(uint16_t& rawX,uint16_t& rawY,uint16_t& quality) {
  uint16_t x1=0,y1=0,x2=0,y2=0,x3=0,y3=0;
  sampleTouchCoordinates(x1,y1);delayMicroseconds(90);
  sampleTouchCoordinates(x2,y2);delayMicroseconds(90);
  sampleTouchCoordinates(x3,y3);
  restoreTouchSharedPins();

  rawX=touchMedian3(x1,x2,x3);
  rawY=touchMedian3(y1,y2,y3);
  uint16_t spreadX=max(x1,max(x2,x3))-min(x1,min(x2,x3));
  uint16_t spreadY=max(y1,max(y2,y3))-min(y1,min(y2,y3));

  bool inside=rawX>=18&&rawX<=1005&&rawY>=18&&rawY<=1005;
  uint16_t stabilityLimit=calibrating?180:100;
  bool stable=spreadX<=stabilityLimit&&spreadY<=stabilityLimit;
  quality=uint16_t(constrain(1023-int(spreadX+spreadY)*2,0,1023));
  return inside&&stable;
}

static bool validTouchAffine(const TouchCalibration& affine) {
  const float values[6]={affine.ax,affine.bx,affine.cx,affine.ay,affine.by,affine.cy};
  for(float value:values)if(!isfinite(value))return false;
  if(fabsf(affine.ax)>10.0f||fabsf(affine.bx)>10.0f||
     fabsf(affine.ay)>10.0f||fabsf(affine.by)>10.0f||
     fabsf(affine.cx)>10000.0f||fabsf(affine.cy)>10000.0f)return false;
  return fabsf(affine.ax)+fabsf(affine.bx)>0.02f&&
         fabsf(affine.ay)+fabsf(affine.by)>0.02f;
}

static void mapTouchBase(uint16_t rawX,uint16_t rawY,int& x,int& y) {
  if(touchCalValid) {
    x=int(lroundf(touchCal.ax*rawX+touchCal.bx*rawY+touchCal.cx));
    y=int(lroundf(touchCal.ay*rawX+touchCal.by*rawY+touchCal.cy));
  } else {
    x=int(map(long(rawY),942L,139L,0L,479L));
    y=int(map(long(rawX),136L,907L,0L,319L));
  }
  x=constrain(x,0,479);y=constrain(y,0,319);
}

static void mapTouch(uint16_t rawX,uint16_t rawY,int& x,int& y) {
  mapTouchBase(rawX,rawY,x,y);
  Pixel::orientTouch(displayMode,x,y);
}

void drawCalibrationTarget() {
  static const int tx[4]={40,439,439,40};
  static const int ty[4]={40,40,279,279};
  panel.fillScreen(0x0000);
  panel.setTextColor(0xFFFF);
  panel.setTextSize(2);
  panel.setCursor(130,12);
  panel.print("TOUCH CALIBRATION");
  panel.setTextSize(1);
  panel.setCursor(190,38);
  panel.print("Point ");
  panel.print(calPoint+1);
  panel.print(" / 4");
  int x=tx[calPoint],y=ty[calPoint];
  panel.drawCircle(x,y,14,0xFFFF);
  panel.drawCircle(x,y,15,0xFFFF);
  panel.drawLine(x-24,y,x+24,y,0xFFFF);
  panel.drawLine(x,y-24,x,y+24,0xFFFF);
}

static bool finishCalibration() {
  const float r0x=calRawX[0],r0y=calRawY[0];
  const float r1x=calRawX[1],r1y=calRawY[1];
  const float r2x=calRawX[2],r2y=calRawY[2];
  const float r3x=calRawX[3],r3y=calRawY[3];

  const float centerX=(r0x+r1x+r2x+r3x)*0.25f;
  const float centerY=(r0y+r1y+r2y+r3y)*0.25f;
  const float ux=(r1x+r2x-r0x-r3x)*0.25f;
  const float uy=(r1y+r2y-r0y-r3y)*0.25f;
  const float vx=(r2x+r3x-r0x-r1x)*0.25f;
  const float vy=(r2y+r3y-r0y-r1y)*0.25f;
  const float determinant=ux*vy-uy*vx;
  if(!isfinite(determinant)||fabsf(determinant)<1500.0f)return false;

  constexpr float centerScreenX=(40.0f+439.0f)*0.5f;
  constexpr float centerScreenY=(40.0f+279.0f)*0.5f;
  constexpr float halfScreenX=(439.0f-40.0f)*0.5f;
  constexpr float halfScreenY=(279.0f-40.0f)*0.5f;

  TouchCalibration next{};
  next.magic=touchCal.magic;
  next.ax=halfScreenX*vy/determinant;
  next.bx=-halfScreenX*vx/determinant;
  next.cx=centerScreenX-next.ax*centerX-next.bx*centerY;
  next.ay=-halfScreenY*uy/determinant;
  next.by=halfScreenY*ux/determinant;
  next.cy=centerScreenY-next.ay*centerX-next.by*centerY;
  if(!validTouchAffine(next))return false;

  static const float targetX[4]={40,439,439,40};
  static const float targetY[4]={40,40,279,279};
  float worst=0;
  for(int i=0;i<4;i++) {
    float mx=next.ax*calRawX[i]+next.bx*calRawY[i]+next.cx;
    float my=next.ay*calRawX[i]+next.by*calRawY[i]+next.cy;
    float dx=mx-targetX[i],dy=my-targetY[i];
    worst=max(worst,sqrtf(dx*dx+dy*dy));
  }
  if(!isfinite(worst)||worst>55.0f)return false;
  if(prefs.putBytes("touchcal2",&next,sizeof(next))!=sizeof(next))return false;

  touchCal=next;
  touchCalValid=true;
  return true;
}

static void resetTouchState() {
  touchStablePressed=false;
  touchPressConfirmations=0;
  touchReleaseMisses=0;
  touchCandidateX=touchCandidateY=0;
  touchPressStartedAt=0;
  calSampleCount=0;
}

static void beginCalibration() {
  calibrating=true;
  calPoint=0;
  memset(calRawX,0,sizeof(calRawX));
  memset(calRawY,0,sizeof(calRawY));
  resetTouchState();
  panel.orientation(0);
  drawCalibrationTarget();
}

void scanTouch(uint32_t now) {
  constexpr uint32_t pollMs=20;
  constexpr uint8_t confirmCount=3;
  constexpr uint8_t releaseMissCount=4;
  constexpr uint16_t confirmMove=70;

  if(uint32_t(now-lastTouch)<pollMs)return;
  lastTouch=now;

  uint16_t rawX=0,rawY=0,quality=0;
  bool pressed=readTouchRaw(rawX,rawY,quality);

  if(touchDiag&&uint32_t(now-lastTouchDiag)>=80) {
    lastTouchDiag=now;
    int dx=-1,dy=-1;
    if(pressed)mapTouch(rawX,rawY,dx,dy);
    emit("E|TOUCHRAW|"+String(pressed?1:0)+"|"+String(rawX)+"|"+
         String(rawY)+"|"+String(quality)+"|"+String(dx)+"|"+String(dy));
  }

  if(pressed) {
    touchReleaseMisses=0;
    if(!touchStablePressed) {
      if(touchPressConfirmations==0)touchPressConfirmations=1;
      else if(touchDelta(rawX,touchCandidateX)<=confirmMove&&
              touchDelta(rawY,touchCandidateY)<=confirmMove) {
        if(touchPressConfirmations<confirmCount)touchPressConfirmations++;
      } else touchPressConfirmations=1;
      touchCandidateX=rawX;touchCandidateY=rawY;
      if(touchPressConfirmations<confirmCount)return;
      touchStablePressed=true;
      touchPressConfirmations=0;
      touchPressStartedAt=now;
      calSampleCount=0;
      userActivity();
    }

    if(calibrating) {
      if(calSampleCount<7) {
        calSamplesX[calSampleCount]=rawX;
        calSamplesY[calSampleCount]=rawY;
        calSampleCount++;
      }
      return;
    }

    int x=0,y=0;
    mapTouch(rawX,rawY,x,y);
    if(now==touchPressStartedAt) {
      if(y>=280) {
        int profileBank=(profile/5)*5;
        selectProfile(profileBank+constrain(x/96,0,4));
      }
      emit("E|TOUCH|"+String(x)+"|"+String(y)+"|"+String(quality));
    }
    return;
  }

  touchPressConfirmations=0;
  touchCandidateX=touchCandidateY=0;
  if(!touchStablePressed){touchReleaseMisses=0;return;}
  if(touchReleaseMisses<releaseMissCount)touchReleaseMisses++;
  if(touchReleaseMisses<releaseMissCount)return;

  touchReleaseMisses=0;
  touchStablePressed=false;

  if(calibrating) {
    if(calSampleCount<3) {
      emit("E|CALWAIT|"+String(calPoint+1)+"|HOLD");
      calSampleCount=0;
      return;
    }
    for(uint8_t i=calSampleCount;i<7;i++) {
      calSamplesX[i]=calSamplesX[calSampleCount-1];
      calSamplesY[i]=calSamplesY[calSampleCount-1];
    }
    calRawX[calPoint]=touchMedian7(calSamplesX);
    calRawY[calPoint]=touchMedian7(calSamplesY);
    emit("E|CALPOINT|"+String(calPoint+1)+"|"+String(calRawX[calPoint])+"|"+
         String(calRawY[calPoint]));
    calSampleCount=0;
    calPoint++;

    if(calPoint>=4) {
      bool ok=finishCalibration();
      calibrating=false;
      panel.orientation(displayMode);
      displayDirty=true;dirtyTiles=255;
      if(ok) {
        emit("E|CALDONE|AFFINE|"+String(touchCal.ax,6)+"|"+String(touchCal.bx,6)+"|"+
             String(touchCal.cx,3)+"|"+String(touchCal.ay,6)+"|"+
             String(touchCal.by,6)+"|"+String(touchCal.cy,3));
      } else emit("E|CALFAIL|BAD_GEOMETRY");
      userActivity();
    } else drawCalibrationTarget();
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

void drawMusicScreen() {
  panel.fillScreen(0x0843);
  panel.setTextColor(0xFFFF);
  panel.setTextSize(1);
  panel.setCursor(20,18);
  panel.print("NOW PLAYING");
  panel.setCursor(400,18);
  panel.print(musicPlaying?"PLAY":"PAUSE");

  panel.setTextSize(3);
  panel.setCursor(20,78);
  char titleLine[25]{};
  strncpy(titleLine,musicTitle,24);
  panel.print(titleLine[0]?titleLine:"No active media");

  panel.setTextSize(2);
  panel.setTextColor(0xBDF7);
  panel.setCursor(20,138);
  char artistLine[37]{};
  strncpy(artistLine,musicArtist,36);
  panel.print(artistLine[0]?artistLine:"--");

  panel.setTextSize(1);
  panel.setTextColor(0x7BEF);
  panel.setCursor(20,176);
  char albumLine[55]{};
  snprintf(albumLine,sizeof(albumLine),"Album: %.42s",musicAlbum[0]?musicAlbum:"--");
  panel.print(albumLine);

  panel.setTextColor(0xFFFF);
  panel.setTextSize(1);
  panel.drawRect(20,210,440,54,0x7BEF);
  panel.setCursor(34,230);
  panel.print("SMTC Music Plugin  |  HID keys remain active");
}

void render() {
  if(!panelAwake||mediaActive||calibrating||monitorActive)return;
  if(musicActive) {
    if(displayDirty){drawMusicScreen();displayDirty=false;}
    return;
  }
  if(displayDirty) {
    panel.fillScreen(0x0843);
    panel.setTextSize(2);
    panel.setTextColor(0xFFFF);
    panel.setCursor(12,10);
    panel.print("PIXEL PRO 2.0");
    int profileBank=(profile/5)*5;
    for(int slot=0;slot<5;++slot) {
      int p=profileBank+slot;
      panel.fillRect(slot*96+2,282,92,36,p==profile?0x04BF:0x18C6);
      panel.setCursor(slot*96+13,292);
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
  if(!flashReady||!mediaFrame||calibrating||upload.active||mediaActive||monitorActive||musicActive)return;
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
  } else if(upload.script) {
    if(check&&check.size()<=sizeof(scriptData)&&check.size()>=6) {
      uint16_t size=check.size();
      valid=check.read(scriptData,size)==size&&validateScriptBytes(scriptData,size);
    }
  } else valid=readMediaHeader(check);
  if(check)check.close();
  if(!valid){abortUpload("FORMAT");return;}

  char target[24];
  if(upload.icon)snprintf(target,sizeof(target),"/i%u%u.pxi",unsigned(upload.iconProfile),unsigned(upload.iconKey));
  else if(upload.script)snprintf(target,sizeof(target),"/s%u%u.pxs",unsigned(upload.scriptProfile),unsigned(upload.scriptKey));
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

uint32_t keymapHash() {
  uint32_t hash=2166136261UL;
  auto add=[&](uint8_t value){hash^=value;hash*=16777619UL;};
  for(int p=0;p<Pixel::Profiles;p++)for(int k=0;k<Pixel::Keys;k++) {
    const auto& binding=config.bindings[p][k];
    add(uint8_t(binding.type));
    add(uint8_t(binding.code));add(uint8_t(binding.code>>8));
    add(binding.modifiers);
    add(uint8_t(binding.color));add(uint8_t(binding.color>>8));
    for(size_t i=0;i<sizeof(binding.label);i++)add(uint8_t(binding.label[i]));
  }
  return hash;
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
    ok("PIXELPRO2|2.4.0|25|8|HX8357B|HID,CDC,RGB,TOUCH,TOUCHCAL,TOUCHDIAG,SD,PANEL,MEDIA,SAVER,ICON,MONITOR,MUSIC,MOUSE,SCRIPT");
    return;
  }
  if(cmd=="INFO"&&n==2) {
    char id[12];
    snprintf(id,sizeof(id),"%08lX",uint32_t(ESP.getEfuseMac()));
    ok("2.4.0|"+String(id));return;
  }
  if(cmd=="KEYHASH"&&n==2){ok(String(keymapHash()));return;}
  if(cmd=="PANEL"&&n==2){ok(String(displayMode));return;}
  if(cmd=="DISPLAY"&&n==3&&Pixel::number(tokens[2],3,v)) {
    if(prefs.putUChar("orient2",v)!=1){error("STORAGE");return;}
    displayMode=v;
    panel.orientation(displayMode);
    displayDirty=true;dirtyTiles=255;
    ok("DISPLAY");return;
  }
  if(cmd=="STATE"&&n==2) {
    ok(String(profile)+"|"+String(config.brightness)+"|"+String(saverSeconds)+"|"+String(screenOffSeconds));
    return;
  }
  if(cmd=="SCREENOFF"&&n==3&&Pixel::number(tokens[2],900,v)&&
     (v==0||v==30||v==300||v==900)) {
    if(prefs.putUShort("screenoff",v)!=2){error("STORAGE");return;}
    screenOffSeconds=v;
    userActivity();
    ok("SCREENOFF");return;
  }
  if(cmd=="SAVER"&&n==3&&Pixel::number(tokens[2],3600,v)) {
    if(prefs.putUShort("saver",v)!=2){error("STORAGE");return;}
    saverSeconds=v;
    ok("SAVER");return;
  }
  if(cmd=="MUSIC"&&(n==6||n==7)&&strcmp(tokens[2],"SET")==0&&
     Pixel::number(tokens[3],1,v)&&strlen(tokens[4])<=48&&strlen(tokens[5])<=48&&
     (n==6||strlen(tokens[6])<=48)) {
    if(calibrating||upload.active){error("BUSY");return;}
    stopSaver();
    monitorActive=false;
    musicActive=true;
    musicPlaying=v!=0;
    strncpy(musicTitle,tokens[4],sizeof(musicTitle)-1);
    strncpy(musicArtist,tokens[5],sizeof(musicArtist)-1);
    if(n==7)strncpy(musicAlbum,tokens[6],sizeof(musicAlbum)-1);
    else musicAlbum[0]=0;
    musicTitle[sizeof(musicTitle)-1]=0;
    musicArtist[sizeof(musicArtist)-1]=0;
    musicAlbum[sizeof(musicAlbum)-1]=0;
    displayDirty=true;
    userActivity();
    ok("MUSIC");return;
  }
  if(cmd=="MUSIC"&&n==3&&strcmp(tokens[2],"OFF")==0) {
    musicActive=false;
    displayDirty=true;dirtyTiles=255;
    ok("OFF");return;
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
    musicActive=false;
    monitorCpu=p;monitorGpu=k;monitorRam=v;monitorDisk=m;monitorNet=color;
    monitorCpuTemp=a;monitorGpuTemp=b;
    monitorActive=true;
    drawMonitorScreen();
    ok("MONITOR");
    return;
  }
  if(cmd=="PROFILE"&&n==3&&Pixel::number(tokens[2],Pixel::Profiles-1,p)) {
    selectProfile(p);ok("PROFILE");return;
  }
  if(cmd=="GET"&&n==4&&Pixel::number(tokens[2],Pixel::Profiles-1,p)&&Pixel::number(tokens[3],7,k)) {
    auto& b=config.bindings[p][k];
    ok(String(b.type)+"|"+String(b.code)+"|"+String(b.modifiers)+"|"+String(b.color)+"|"+b.label);
    return;
  }
  if(cmd=="SET"&&n==9&&Pixel::number(tokens[2],Pixel::Profiles-1,p)&&Pixel::number(tokens[3],7,k)&&
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
    beginCalibration();
    ok("STARTED");
    return;
  }
  if(cmd=="TOUCHCAL"&&n==3&&strcmp(tokens[2],"GET")==0) {
    if(!touchCalValid){ok("DEFAULT");return;}
    ok("AFFINE|"+String(touchCal.ax,6)+"|"+String(touchCal.bx,6)+"|"+
       String(touchCal.cx,3)+"|"+String(touchCal.ay,6)+"|"+
       String(touchCal.by,6)+"|"+String(touchCal.cy,3));
    return;
  }
  if(cmd=="TOUCHCAL"&&n==3&&strcmp(tokens[2],"RESET")==0) {
    prefs.remove("touchcal2");
    touchCalValid=false;
    ok("RESET");return;
  }
  if(cmd=="TOUCHDIAG"&&n==3&&(strcmp(tokens[2],"ON")==0||strcmp(tokens[2],"OFF")==0)) {
    touchDiag=strcmp(tokens[2],"ON")==0;
    ok(touchDiag?"ON":"OFF");
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
     Pixel::number(tokens[3],Pixel::Profiles-1,p)&&Pixel::number(tokens[4],7,k)) {
    if(!ensureFlash(false)){ok("DELETED");return;}
    char path[16];
    snprintf(path,sizeof(path),"/i%u%u.pxi",unsigned(p),unsigned(k));
    SPIFFS.remove(path);
    if(p==profile)dirtyTiles|=1<<k;
    ok("DELETED");return;
  }
  if(cmd=="ICON"&&n==7&&strcmp(tokens[2],"BEGIN")==0&&
     Pixel::number(tokens[3],Pixel::Profiles-1,p)&&Pixel::number(tokens[4],7,k)&&
     Pixel::number(tokens[5],8192,v)&&Pixel::number(tokens[6],0xFFFFFFFFUL,m)) {
    if(!ensureFlash(true)||v<8){error("ICON");return;}
    stopSaver();
    if(upload.file)upload.file.close();
    SPIFFS.remove("/upload.tmp");
    upload.file=SPIFFS.open("/upload.tmp",FILE_WRITE);
    if(!upload.file){error("FLASH");return;}
    upload.active=true;
    upload.icon=true;
    upload.script=false;
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

  if(cmd=="SCRIPT"&&n==5&&strcmp(tokens[2],"DELETE")==0&&
     Pixel::number(tokens[3],Pixel::Profiles-1,p)&&Pixel::number(tokens[4],7,k)) {
    if(!ensureFlash(false)){ok("DELETED");return;}
    char path[16];
    snprintf(path,sizeof(path),"/s%u%u.pxs",unsigned(p),unsigned(k));
    SPIFFS.remove(path);
    ok("DELETED");return;
  }
  if(cmd=="SCRIPT"&&n==7&&strcmp(tokens[2],"BEGIN")==0&&
     Pixel::number(tokens[3],Pixel::Profiles-1,p)&&Pixel::number(tokens[4],7,k)&&
     Pixel::number(tokens[5],8192,v)&&Pixel::number(tokens[6],0xFFFFFFFFUL,m)) {
    if(!ensureFlash(true)||v<7){error("SCRIPT");return;}
    stopSaver();
    if(upload.file)upload.file.close();
    SPIFFS.remove("/upload.tmp");
    upload.file=SPIFFS.open("/upload.tmp",FILE_WRITE);
    if(!upload.file){error("FLASH");return;}
    upload.active=true;
    upload.icon=false;
    upload.script=true;
    upload.scriptProfile=p;
    upload.scriptKey=k;
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
    upload.script=false;
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
  mouse.begin();
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
  restoreTouchSharedPins();

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
  runScript(now);
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

  if(panelAwake&&!calibrating&&screenOffSeconds>0&&
     uint32_t(now-lastInput)>=uint32_t(screenOffSeconds)*1000UL) {
    stopSaver();
    panel.displayOff();
    panelAwake=false;
  }
  if(panelAwake&&!calibrating&&!mediaActive&&saverSeconds>0&&
     uint32_t(now-lastInput)>=uint32_t(saverSeconds)*1000UL)
    startSaver(now);
  if(panelAwake&&mediaActive)playSaver(now);
  else render();

  delay(1);
}
