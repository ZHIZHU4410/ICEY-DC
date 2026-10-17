using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using dc.h2d;
using dc.h3d.mat;
using dc.hxd;
namespace EarlyPlaytest;
public sealed class DashDisplay:IDisposable {
 readonly FrameSlot slot=new(@"Local\IceyDCBridge_V1_DASH");readonly byte[] data=new byte[FrameSlot.Size];readonly StreamWriter log;Texture texture;Pixels pixels;dc.haxe.io.Bytes bytes;Bitmap bitmap;long frame;int generation;bool disposed;
 public DashDisplay(){log=new StreamWriter(Path.Combine(EpSession.Output,"dc_dash.csv"));log.AutoFlush=true;log.WriteLine("utc,frameId,playerFrameId,tickId,generation,charges,capacity,nativeAlpha,readyQpc,useQpc,x,y,hash");}
 public void Hide(){if(bitmap!=null)bitmap.visible=false;}
 public void Update(){if(disposed)return;if(!CarrierGuard.Active){Hide();return;}var h=CarrierGuard.Hero;if(h?._level?.game?.hud?.root==null)return;var scene=h._level.game.hud.root.getScene();int g=HostWorld.Instance.Generation;if(generation!=g){Hide();generation=g;}if(slot.Read(data,frame)&&BitConverter.ToInt32(data,32)==g){if(BitConverter.ToInt32(data,36)!=512||BitConverter.ToInt32(data,40)!=128)throw new InvalidOperationException("Native dash dimensions mismatch");if(texture==null){var format=new PixelFormat.RGBA();texture=new Texture(512,128,null,format,null);texture.preventAutoDispose();bytes=dc.haxe.io.Bytes.Class.alloc.Invoke(512*128*4);int offset=0;pixels=new Pixels(512,128,bytes,format,HaxeProxy.Runtime.Ref<int>.From(ref offset));}Marshal.Copy(data,FrameSlot.Header,bytes.b,512*128*4);int mip=0,layerIndex=0;texture.uploadPixels(pixels,ref mip,ref layerIndex);if(bitmap==null||bitmap.parent!=scene){bitmap?.remove();bitmap=new Bitmap(Tile.Class.fromTexture.Invoke(texture),scene);bitmap.blendMode=new BlendMode.AlphaAdd();}frame=BitConverter.ToInt64(data,8);bitmap.scaleX=bitmap.scaleY=.5;bitmap.visible=h._level.game.hud.root.visible;log.WriteLine(string.Join(",",DateTime.UtcNow.ToString("o"),frame,BitConverter.ToInt64(data,24),BitConverter.ToInt64(data,16),g,BitConverter.ToInt32(data,96),BitConverter.ToInt32(data,100),BitConverter.ToDouble(data,112),BitConverter.ToInt64(data,48),Stopwatch.GetTimestamp(),scene.width*.5-128,scene.height-80,BitConverter.ToUInt32(data,180)));}if(bitmap!=null){bitmap.x=scene.width*.5-128;bitmap.y=scene.height-80;}}
 public void Dispose(){if(disposed)return;disposed=true;bitmap?.remove();texture?.dispose();slot.Dispose();log.Dispose();}
}
