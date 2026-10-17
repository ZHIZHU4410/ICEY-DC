using System;
using System.Runtime.InteropServices;
namespace EarlyPlaytest {
// Single newest-frame mailbox, fixed storage, no frame queue. A busy reader/writer skips.
public sealed class FrameSlot:IDisposable {
 public const int Header=320, W=256,H=256,Size=Header+W*H*4;
 public const int PlayerW=1024,PlayerH=1024,PlayerSize=Header+PlayerW*PlayerH*4; public const string PlayerMailbox=@"Local\IceyDCBridge_CPU_V101_1024"; readonly int capacity;
 IntPtr map,view,mutex; public int Busy;
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateFileMappingW(IntPtr f,IntPtr s,uint p,uint hi,uint lo,string n);
 [DllImport("kernel32.dll")]static extern IntPtr MapViewOfFile(IntPtr h,uint a,uint hi,uint lo,UIntPtr s);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateMutexW(IntPtr s,bool o,string n);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr h,uint ms);
 [DllImport("kernel32.dll")]static extern bool ReleaseMutex(IntPtr h);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll")]static extern bool UnmapViewOfFile(IntPtr v);
 public FrameSlot():this(PlayerMailbox,PlayerSize){} public FrameSlot(string name):this(name,Size){} public FrameSlot(string name,int capacity){this.capacity=capacity;map=CreateFileMappingW(new IntPtr(-1),IntPtr.Zero,4,0,(uint)capacity,name);view=MapViewOfFile(map,0xF001F,0,0,new UIntPtr((uint)capacity));mutex=CreateMutexW(IntPtr.Zero,false,name+"_Mutex");if(map==IntPtr.Zero||view==IntPtr.Zero||mutex==IntPtr.Zero)throw new InvalidOperationException("CPU frame mailbox unavailable "+Marshal.GetLastWin32Error());}
 bool Enter(){uint r=WaitForSingleObject(mutex,0);if(r==0||r==128)return true;Busy++;return false;}
 public bool Write(byte[] b){if(b.Length<capacity)throw new ArgumentException("mailbox write buffer too small");if(!Enter())return false;try{Marshal.Copy(b,0,view,capacity);return true;}finally{ReleaseMutex(mutex);}}
 public bool Read(byte[] b,long previous){if(b.Length<capacity)throw new ArgumentException("mailbox read buffer too small");if(!Enter())return false;try{if(Marshal.ReadInt32(view)!=0x34504645||Marshal.ReadInt64(view,8)<=previous)return false;Marshal.Copy(view,b,0,capacity);return true;}finally{ReleaseMutex(mutex);}}
 // Guest telemetry only needs the immutable metadata, not another four-megabyte pixel copy.
 public bool ReadHeader(byte[] b,long previous){if(b.Length<Header)throw new ArgumentException("mailbox header buffer too small");if(!Enter())return false;try{if(Marshal.ReadInt32(view)!=0x34504645||Marshal.ReadInt64(view,8)<=previous)return false;Marshal.Copy(view,b,0,Header);return true;}finally{ReleaseMutex(mutex);}}
 public void Dispose(){if(view!=IntPtr.Zero)UnmapViewOfFile(view);if(map!=IntPtr.Zero)CloseHandle(map);if(mutex!=IntPtr.Zero)CloseHandle(mutex);view=map=mutex=IntPtr.Zero;}
 public static void I(byte[] b,int o,int v){unchecked{for(int k=0;k<4;k++)b[o+k]=(byte)(v>>(8*k));}}
 public static void L(byte[] b,int o,long v){unchecked{for(int k=0;k<8;k++)b[o+k]=(byte)(v>>(8*k));}}
 public static void D(byte[] b,int o,double v){L(b,o,BitConverter.DoubleToInt64Bits(v));}
}
}
