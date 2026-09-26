#pragma once
#include <stdint.h>
#include <stddef.h>
#include <string.h>
namespace Pixel {
constexpr int Profiles=5, Keys=8;
struct Binding {
  char type; // K keyboard usage, C consumer usage, H host action, P profile, D disabled
  uint16_t code;
  uint8_t modifiers;
  uint16_t color;
  char label[13];
};
inline bool number(const char* s, uint32_t limit, uint32_t& value) {
  if (!s || !*s) return false;
  value=0;
  for (;*s;++s) {
    if (*s<'0'||*s>'9') return false;
    uint32_t digit=uint32_t(*s-'0');
    if (digit>limit || value>(limit-digit)/10) return false;
    value=value*10+digit;
  }
  return true;
}
inline bool valid(const Binding& b) {
  if (!memchr(b.label,0,sizeof(b.label))) return false;
  for(const char* p=b.label;*p;++p) if(*p<32||*p>126||*p=='|') return false;
  switch(b.type) {
    case 'K': return b.code>=4 && b.code<=115;
    case 'C': return !b.modifiers && (b.code==0xE9||b.code==0xEA||b.code==0xE2||b.code==0xCD||b.code==0xB5||b.code==0xB6||b.code==0xB7);
    case 'H': return b.code==0 && !b.modifiers;
    case 'P': return b.code<Profiles && !b.modifiers;
    case 'D': return b.code==0 && !b.modifiers;
    default: return false;
  }
}
struct Debounce {
  bool candidate=false, stable=false;
  uint32_t since=0;
  bool update(bool raw,uint32_t now) {
    if(raw!=candidate) {candidate=raw;since=now;}
    if(stable!=candidate && uint32_t(now-since)>=12) {stable=candidate;return true;}
    return false;
  }
};
// Bound each line and discard all bytes of an oversized frame until newline.
template<size_t N> struct LineBuffer {
  char text[N]{}; size_t size=0; bool overflow=false;
  int feed(char c) {
    if(c=='\r') return 0;
    if(c=='\n') {text[size]=0;int result=overflow?-1:1;size=0;overflow=false;return result;}
    if(c==0) {overflow=true;return 0;}
    if(!overflow) {if(size+1<N) text[size++]=c;else overflow=true;}
    return 0;
  }
};
}
