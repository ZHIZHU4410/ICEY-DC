using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using dc.h2d;
using dc.h3d.mat;
using dc.hxd;
namespace EarlyPlaytest;
public sealed class HudDisplay:IDisposable {
 readonly FrameSlot slot=new(@"Local\IceyDCBridge_FP50_HUD");readonly byte[] data=new byte[FrameSlot.Size];readonly StreamWriter log;Texture texture;Pixels pixels;dc.haxe.io.Bytes bytes;Bitmap bitmap;long frame;bool disposed;
 public HudDisplay(){log=new StreamWriter(Path.Combine(EpSession.Output,"dc_hud.csv"));log.AutoFlush=true;log.WriteLine("utc,frameId,playerFrameId,tickId,generation,hp,maxHp,energy,maxEnergy,readyQpc,useQpc,x,y,hash");}
 public void Hide(){if(bitmap!=null)bitmap.visible=false;}
 public void Update(){if(disposed)return;if(!CarrierGuard.Active){Hide();return;}var h=CarrierGuard.Hero;if(h?._level?.game?.hud?.root==null)return;if(!slot.Read(data,frame))return;if(BitConverter.ToInt32(data,32)!=HostWorld.Instance.Generation)return;if(BitConverter.ToInt32(data,36)!=512||BitConverter.ToInt32(data,40)!=128)throw new InvalidOperationException("Native HUD dimensions mismatch");if(texture==null){var format=new PixelFormat.RGBA();texture=new Texture(512,128,null,format,null);texture.preventAutoDispose();bytes=dc.haxe.io.Bytes.Class.alloc.Invoke(512*128*4);int offset=0;pixels=new Pixels(512,128,bytes,format,HaxeProxy.Runtime.Ref<int>.From(ref offset));}
 Marshal.Copy(data,FrameSlot.Header,bytes.b,512*128*4);int mip=0,layer=0;texture.uploadPixels(pixels,ref mip,ref layer);var scene=h._level.game.hud.root.getScene();if(bitmap==null||bitmap.parent!=scene){bitmap?.remove();bitmap=new Bitmap(Tile.Class.fromTexture.Invoke(texture),scene);bitmap.blendMode=new BlendMode.AlphaAdd();}bitmap.scaleX=bitmap.scaleY=.625;bitmap.x=scene.width-330;bitmap.y=12;bitmap.visible=h._level.game.hud.root.visible;frame=BitConverter.ToInt64(data,8);log.WriteLine(string.Join(",",DateTime.UtcNow.ToString("o"),frame,BitConverter.ToInt64(data,24),BitConverter.ToInt64(data,16),BitConverter.ToInt32(data,32),BitConverter.ToInt32(data,96),BitConverter.ToInt32(data,100),BitConverter.ToInt32(data,104),BitConverter.ToInt32(data,108),BitConverter.ToInt64(data,48),Stopwatch.GetTimestamp(),bitmap.x,bitmap.y,BitConverter.ToUInt32(data,180)));
 }
 public void Dispose(){if(disposed)return;disposed=true;bitmap?.remove();texture?.dispose();slot.Dispose();log.Dispose();}
}
