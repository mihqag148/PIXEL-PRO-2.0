#include <cassert>
#include <iostream>
#include "../firmware/PixelPro2/Model.h"
#include "../firmware/PixelPro2/DisplayMode.h"
int main() {
  assert(Pixel::displayMadctl(0)==0x68);
  assert(Pixel::displayMadctl(1)==0xA8);
  assert(Pixel::displayMadctl(2)==0xE8);
  assert(Pixel::displayMadctl(3)==0x28);
  int x=0,y=0;Pixel::orientTouch(1,x,y);assert(x==479&&y==319);
  for(int mode=0;mode<4;++mode)for(int px:{0,95,96,479})for(int py:{0,281,319}) {
    int tx=px,ty=py;Pixel::orientTouch(mode,tx,ty);
    assert(tx>=0&&tx<480&&ty>=0&&ty<320);
    Pixel::orientTouch(mode,tx,ty);assert(tx==px&&ty==py);
  }
  uint32_t v;assert(Pixel::number("65535",65535,v)&&v==65535);
  for(auto s:{"65536","42949672960","-1",""," 1","1x"})assert(!Pixel::number(s,65535,v));
  assert(!Pixel::number("9",4,v));
  Pixel::LineBuffer<8> b;
  for(char c:std::string("12345678"))assert(b.feed(c)==0);
  assert(b.feed('\n')==-1);b.feed('O');b.feed('K');assert(b.feed('\n')==1);assert(!strcmp(b.text,"OK"));
  Pixel::Debounce d;assert(!d.update(true,0));assert(!d.update(false,5));assert(!d.update(true,8));
  assert(!d.update(true,19));assert(d.update(true,20)&&d.stable);
  d.since=0xFFFFFFFA;d.candidate=false;assert(d.update(false,8)&&!d.stable);
  Pixel::Binding key{'K',4,1,0,"Copy"};assert(Pixel::valid(key));
  key.code=65535;assert(!Pixel::valid(key));key.type='P';key.code=5;key.modifiers=0;assert(!Pixel::valid(key));
  key.code=4;assert(Pixel::valid(key));strcpy(key.label,"bad|label");assert(!Pixel::valid(key));
  std::cout<<"Model tests passed\n";
}
