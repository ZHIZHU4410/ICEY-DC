using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using dc.h2d;
using dc.h3d.mat;
using dc.hxd;
namespace EarlyPlaytest;
public sealed class CpuDisplay:IDisposable { public long CurrentFrameId=>frame;public long CurrentReadyQpc=>BitConverter.ToInt64(packet,48);public long CurrentTickId=>BitConverter.ToInt64(packet,16);
 public static CpuDisplay Instance;FrameSlot slot;readonly byte[] packet=new byte[FrameSlot.PlayerSize];
 Texture texture;Bitmap bitmap;Pixels pixels;dc.haxe.io.Bytes bytes;StreamWriter log,presents,quality;double nextQuality;
 dc.pr.Level hostLevel;dc.en.Hero hero;long frame;int gen;double anchorX,anchorY;long useQpc,sourceQpc,drawnFrame,drawnSequence,drawQpc;bool disposed;
 Serilog.ILogger logger;static bool hooksInstalled;
 public CpuDisplay(Serilog.ILogger logger){this.logger=logger;Instance=this;quality=new StreamWriter(Path.Combine(Output,"display_quality.csv"));quality.AutoFlush=true;quality.WriteLine("utc,frameId,sourceW,sourceH,renderW,renderH,sceneW,sceneH,worldScaleX,worldScaleY,minScale");slot=new FrameSlot();string stamp=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");log=new StreamWriter(Path.Combine(InputActionOut,"dc_frames_"+stamp+".csv"));log.WriteLine("utc,frameId,tickId,sequence,generation,sourceQpc,consumeQpc,commitQpc,readyQpc,useQpc,uploadEndQpc,gap,pose,bitmapX,bitmapY,scale,hash");presents=new StreamWriter(Path.Combine(InputActionOut,"host_present_"+stamp+".csv"));presents.AutoFlush=true;log.AutoFlush=true;presents.WriteLine("utc,frameId,sequence,sourceQpc,useQpc,driverPresentReturnedQpc,drawnFrameId,drawnSequence,drawQpc");}
 public static readonly string Output=EpSession.Output; static string InputActionOut=>Output;
 public static void InstallHooks(){if(hooksInstalled)return;hooksInstalled=true;NativeResolutionWorld.Install();dc.h3d.impl.Hook_GlDriver.present+=AfterPresent;dc.h2d.Hook_Bitmap.draw+=AfterDraw;}
 static void AfterDraw(dc.h2d.Hook_Bitmap.orig_draw orig,dc.h2d.Bitmap self,dc.h2d.RenderContext context){orig(self,context);var i=Instance;if(i!=null&&i.bitmap==self){i.drawnFrame=i.frame;i.drawnSequence=BitConverter.ToInt64(i.packet,24);i.drawQpc=Stopwatch.GetTimestamp();double now=i.drawQpc/(double)Stopwatch.Frequency;if(now>=i.nextQuality){i.nextQuality=now+1;var sc=i.hero._level.scroller;var scene=sc.getScene();i.quality.WriteLine(string.Join(",",DateTime.UtcNow.ToString("o"),i.frame,FrameSlot.PlayerW,FrameSlot.PlayerH,context.curWidth,context.curHeight,scene.width,scene.height,sc.scaleX,sc.scaleY,sc.minScale));}}}
 static void AfterPresent(dc.h3d.impl.Hook_GlDriver.orig_present orig,dc.h3d.impl.GlDriver self){orig(self);Instance?.Present();}
 void Present(){if(disposed||frame==0)return;presents.WriteLine(string.Join(",",DateTime.UtcNow.ToString("o"),frame,BitConverter.ToInt64(packet,24),sourceQpc,useQpc,Stopwatch.GetTimestamp(),drawnFrame,drawnSequence,drawQpc));drawnFrame=0;drawnSequence=0;drawQpc=0;if(frame%60==0)presents.Flush();}
 public void Hide(){if(bitmap!=null)bitmap.visible=false;}
 public void Update(){if(disposed)return;var h=ModCore.Modules.Game.Instance.HeroInstance;if(h==null||h.destroyed||h._level==null)return;HostWorld.Instance?.Export(h);if(HostWorld.Instance==null||!HostWorld.Instance.NativeRequested){Hide();return;}if(!slot.Read(packet,frame))return;if(BitConverter.ToInt32(packet,176)!=HostWorld.Instance.Generation){Hide();return;}if(BitConverter.ToInt32(packet,36)!=FrameSlot.PlayerW||BitConverter.ToInt32(packet,40)!=FrameSlot.PlayerH)throw new InvalidOperationException("Player frame size mismatch; install both v1.0.2 plugins and CPU module together");long begin=Stopwatch.GetTimestamp();long next=BitConverter.ToInt64(packet,8);int generation=BitConverter.ToInt32(packet,32);
  if(hero!=h||hostLevel!=h._level||gen!=generation){hero=h;hostLevel=h._level;gen=generation;anchorX=h.cx+h.xr;anchorY=h.cy+h.yr;if(bitmap!=null)bitmap.remove();bitmap=null;}
  if(texture==null){var fmt=new PixelFormat.RGBA();texture=new Texture(FrameSlot.PlayerW,FrameSlot.PlayerH,null,fmt,null);texture.preventAutoDispose();texture.set_filter(new dc.h3d.mat.Filter.Linear());bytes=dc.haxe.io.Bytes.Class.alloc.Invoke(FrameSlot.PlayerW*FrameSlot.PlayerH*4);int offset=0;pixels=new Pixels(FrameSlot.PlayerW,FrameSlot.PlayerH,bytes,fmt,HaxeProxy.Runtime.Ref<int>.From(ref offset));logger.Information("[EP4B] driver={Driver} sprParent={Parent} heroLayer={Layer}",dc.h3d.Engine.Class.CURRENT.driver.GetType().FullName,h.spr.parent.GetType().FullName,h.curLayer);logger.Information("[EP4B] CPU RGBA upload from native ARGB32 RT initialized; no atlas");}
  Marshal.Copy(packet,FrameSlot.Header,bytes.b,FrameSlot.PlayerW*FrameSlot.PlayerH*4);int mip=0,layer=0;texture.uploadPixels(pixels,ref mip,ref layer);
  if(bitmap==null){bitmap=new Bitmap(Tile.Class.fromTexture.Invoke(texture),null);h._level.scroller.addChildAt(bitmap,h.curLayer+1);bitmap.blendMode=new BlendMode.AlphaAdd();}
  double s=BitConverter.ToDouble(packet,136),size=BitConverter.ToDouble(packet,128),cx=BitConverter.ToDouble(packet,112),cy=BitConverter.ToDouble(packet,120),ux=BitConverter.ToDouble(packet,144),uy=BitConverter.ToDouble(packet,152);
  if(BitConverter.ToInt32(packet,176)>0){anchorX=BitConverter.ToDouble(packet,160);anchorY=BitConverter.ToDouble(packet,168);HostWorld.Instance?.Commit(packet,next);int nativeHp=BitConverter.ToInt32(packet,312),nativeMax=BitConverter.ToInt32(packet,316);if(nativeMax>0&&nativeHp>=0&&nativeHp<=nativeMax)h.life=System.Math.Max(1,(int)System.Math.Ceiling(h.maxLife*nativeHp/(double)nativeMax));}
  bitmap.x=(anchorX+(cx-size-ux)/s)*24;bitmap.y=(anchorY-(cy+size-uy)/s)*24;bitmap.scaleX=bitmap.scaleY=2*size*24/(FrameSlot.PlayerW*s);bitmap.visible=true;
  useQpc=begin;sourceQpc=BitConverter.ToInt64(packet,80);long end=Stopwatch.GetTimestamp();string pose=Encoding.ASCII.GetString(packet,184,64).TrimEnd('\0');
  log.WriteLine(string.Join(",",new object[]{DateTime.UtcNow.ToString("o"),next,BitConverter.ToInt64(packet,16),BitConverter.ToInt64(packet,24),generation,sourceQpc,BitConverter.ToInt64(packet,64),BitConverter.ToInt64(packet,56),BitConverter.ToInt64(packet,48),begin,end,frame==0?0:next-frame-1,pose,bitmap.x,bitmap.y,bitmap.scaleX,BitConverter.ToUInt32(packet,180)}));frame=next;if(frame%60==0)log.Flush();
 }
 public void Dispose(){if(disposed)return;disposed=true;NativeResolutionWorld.Dispose();if(bitmap!=null)bitmap.remove();if(texture!=null)texture.dispose();slot?.Dispose();log?.Dispose();presents?.Dispose();quality?.Dispose();Instance=null;}
}
