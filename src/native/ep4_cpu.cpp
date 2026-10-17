#include <windows.h>
#include <d3d11.h>
#include <stdint.h>
#include <string.h>
#include <emmintrin.h>
#include <intrin.h>
#pragma comment(lib,"d3d11.lib")
// CPU readback only. No shared GPU texture or guest/host graphics interop.
static const int Header=320, W=1024,H=1024,Capacity=Header+W*H*4;
struct Pending { LONG id; unsigned char header[Header]; };
static Pending pending[8];
static SRWLOCK lock=SRWLOCK_INIT;
static ID3D11Texture2D *source=nullptr,*staging=nullptr;
static ID3D11Device *device=nullptr;
static ID3D11DeviceContext *context=nullptr;
static D3D11_TEXTURE2D_DESC desc={};
static HANDLE mapping=nullptr,mutexHandle=nullptr;
static unsigned char *view=nullptr;
// Render-thread private scratch: conversion/statistics never hold the shared mailbox mutex.
static unsigned char converted[W*H*4];
static volatile LONG stopped=0,lastError=0,busy=0,pendingCount=0,pendingMax=0;
static int64_t Qpc(){LARGE_INTEGER t;QueryPerformanceCounter(&t);return t.QuadPart;}
static void cleanup(){if(staging){staging->Release();staging=nullptr;}if(context){context->Release();context=nullptr;}if(device){device->Release();device=nullptr;}if(source){source->Release();source=nullptr;}if(view){UnmapViewOfFile(view);view=nullptr;}if(mapping){CloseHandle(mapping);mapping=nullptr;}if(mutexHandle){CloseHandle(mutexHandle);mutexHandle=nullptr;}}
extern "C" __declspec(dllexport) int __cdecl EP4_Configure(void* texture,const wchar_t* mapName){
 if(source)return -10;
 HRESULT hr=((IUnknown*)texture)->QueryInterface(__uuidof(ID3D11Texture2D),(void**)&source);
 if(FAILED(hr)){lastError=(LONG)hr;return -1;}
 source->GetDesc(&desc);
 if(desc.Width!=W||desc.Height!=H||desc.SampleDesc.Count!=1){lastError=-2;return -2;}
 if(desc.Format!=DXGI_FORMAT_R8G8B8A8_TYPELESS&&desc.Format!=DXGI_FORMAT_B8G8R8A8_TYPELESS&&desc.Format!=DXGI_FORMAT_R8G8B8A8_UNORM&&desc.Format!=DXGI_FORMAT_R8G8B8A8_UNORM_SRGB&&desc.Format!=DXGI_FORMAT_B8G8R8A8_UNORM&&desc.Format!=DXGI_FORMAT_B8G8R8A8_UNORM_SRGB){lastError=(LONG)desc.Format;return -3;}
 source->GetDevice(&device);device->GetImmediateContext(&context);
 D3D11_TEXTURE2D_DESC d=desc;d.Usage=D3D11_USAGE_STAGING;d.BindFlags=0;d.CPUAccessFlags=D3D11_CPU_ACCESS_READ;d.MiscFlags=0;
 hr=device->CreateTexture2D(&d,nullptr,&staging);if(FAILED(hr)){lastError=(LONG)hr;return -4;}
 mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,Capacity,mapName);
 if(!mapping){lastError=GetLastError();return -5;}
 view=(unsigned char*)MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,Capacity);
 wchar_t name[256];lstrcpynW(name,mapName,240);lstrcatW(name,L"_Mutex");mutexHandle=CreateMutexW(nullptr,FALSE,name);
 if(!view||!mutexHandle){lastError=GetLastError();return -6;}
 return (int)desc.Format;
}
extern "C" __declspec(dllexport) int __cdecl EP4_Submit(int eventId,const unsigned char* header){
 if(stopped||!source)return 0;
 if(!TryAcquireSRWLockExclusive(&lock)){InterlockedIncrement(&busy);return 0;}
 Pending& p=pending[eventId&7];if(p.id==0){LONG n=InterlockedIncrement(&pendingCount);if(n>pendingMax)InterlockedExchange(&pendingMax,n);}else InterlockedIncrement(&busy);memcpy(p.header,header,Header);p.id=eventId;ReleaseSRWLockExclusive(&lock);return 1;
}
static void __stdcall renderEvent(int eventId){
 if(eventId==-1){InterlockedExchange(&stopped,1);cleanup();return;}
 if(stopped||!source)return;
 unsigned char header[Header];
 if(!TryAcquireSRWLockExclusive(&lock)){InterlockedIncrement(&busy);return;}
 Pending& p=pending[eventId&7];if(p.id!=eventId){ReleaseSRWLockExclusive(&lock);InterlockedIncrement(&busy);return;}
 memcpy(header,p.header,Header);p.id=0;InterlockedDecrement(&pendingCount);ReleaseSRWLockExclusive(&lock);
 int64_t begin=Qpc();context->CopyResource(staging,source);
 D3D11_MAPPED_SUBRESOURCE mapped={};HRESULT hr=context->Map(staging,0,D3D11_MAP_READ,0,&mapped);
 if(FAILED(hr)){lastError=(LONG)hr;return;}
 int64_t mapDone=Qpc();
 uint32_t hash=2166136261u;int zero=0,opaque=0;
 bool bgra=desc.Format==DXGI_FORMAT_B8G8R8A8_TYPELESS||desc.Format==DXGI_FORMAT_B8G8R8A8_UNORM||desc.Format==DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
 const __m128i allOnes=_mm_set1_epi8((char)0xff), zeros=_mm_setzero_si128();
 for(int y=0;y<H;y++){
  const unsigned char* src=(const unsigned char*)mapped.pData+(H-1-y)*mapped.RowPitch;
  unsigned char* dst=converted+y*W*4;
  for(int x=0;x<W;x+=4){
   __m128i pixels=_mm_loadu_si128((const __m128i*)(src+x*4));
   int zeroBits=_mm_movemask_epi8(_mm_cmpeq_epi8(pixels,zeros))&0x8888;
   int opaqueBits=_mm_movemask_epi8(_mm_cmpeq_epi8(pixels,allOnes))&0x8888;
   zero+=(int)__popcnt((unsigned int)zeroBits);opaque+=(int)__popcnt((unsigned int)opaqueBits);
   if(bgra){
    __m128i rb=_mm_or_si128(_mm_slli_epi32(_mm_and_si128(pixels,_mm_set1_epi32(0xff)),16),_mm_and_si128(_mm_srli_epi32(pixels,16),_mm_set1_epi32(0xff)));
    pixels=_mm_or_si128(rb,_mm_and_si128(pixels,_mm_set1_epi32((int)0xff00ff00u)));
   }
   _mm_storeu_si128((__m128i*)(dst+x*4),pixels);
  }
  // Same sampled streaming fingerprint as v1.0.1; every RGBA pixel is preserved.
  for(int x=0;x<W;x+=16){uint32_t word;memcpy(&word,dst+x*4,4);hash=(hash^word)*16777619u;}
 }
 int nonzero=W*H-zero,partial=nonzero-opaque;
 context->Unmap(staging,0);
 DWORD wait=WaitForSingleObject(mutexHandle,0);
 if(wait!=WAIT_OBJECT_0&&wait!=WAIT_ABANDONED){InterlockedIncrement(&busy);return;}
 memcpy(view+Header,converted,W*H*4);
 *(int64_t*)(header+48)=Qpc();*(uint32_t*)(header+180)=hash;
 *(int64_t*)(header+256)=begin;*(int64_t*)(header+264)=mapDone;
 *(int*)(header+272)=zero;*(int*)(header+276)=partial;*(int*)(header+280)=nonzero;*(int*)(header+284)=busy;
 *(int*)(header+304)=pendingCount;*(int*)(header+308)=pendingMax;memcpy(view,header,Header);ReleaseMutex(mutexHandle);
}
extern "C" __declspec(dllexport) void* __cdecl EP4_RenderEvent(){return (void*)renderEvent;}
extern "C" __declspec(dllexport) int __cdecl EP4_Error(){return lastError;}
extern "C" __declspec(dllexport) void __cdecl EP4_Stop(){InterlockedExchange(&stopped,1);}
