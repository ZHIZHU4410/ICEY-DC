using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using dc.h2d;
using dc.h3d.mat;
using dc.hxd;
namespace EarlyPlaytest;
public sealed class ScreenChargeDisplay:IDisposable {
 readonly FrameSlot slot=new(ScreenChargeRules.Mailbox,ScreenChargeRules.Capacity);readonly byte[] data=new byte[ScreenChargeRules.Capacity];Texture texture;Pixels pixels;dc.haxe.io.Bytes bytes;Bitmap bitmap;long frame,ready;
 public void Hide(){if(bitmap!=null)bitmap.visible=false;}
 public void Update(){if(!CarrierGuard.Active){Hide();return;}var h=CarrierGuard.Hero;var scene=h?._level?.game?.hud?.root?.getScene();if(scene==null){Hide();return;}
  if(slot.Read(data,frame)){frame=BitConverter.ToInt64(data,8);ready=BitConverter.ToInt64(data,48);
   if(BitConverter.ToInt32(data,32)!=HostWorld.Instance.Generation||BitConverter.ToInt32(data,56)==0){Hide();return;}
   if(BitConverter.ToInt32(data,36)!=ScreenChargeRules.Width||BitConverter.ToInt32(data,40)!=ScreenChargeRules.Height)throw new InvalidOperationException("Charge capture size mismatch");
   if(texture==null){var format=new PixelFormat.RGBA();texture=new Texture(ScreenChargeRules.Width,ScreenChargeRules.Height,null,format,null);texture.preventAutoDispose();bytes=dc.haxe.io.Bytes.Class.alloc.Invoke(ScreenChargeRules.Width*ScreenChargeRules.Height*4);int offset=0;pixels=new Pixels(ScreenChargeRules.Width,ScreenChargeRules.Height,bytes,format,HaxeProxy.Runtime.Ref<int>.From(ref offset));}
   Marshal.Copy(data,FrameSlot.Header,bytes.b,ScreenChargeRules.Width*ScreenChargeRules.Height*4);int mip=0,layer=0;texture.uploadPixels(pixels,ref mip,ref layer);
   if(bitmap==null||bitmap.parent!=scene){bitmap?.remove();bitmap=new Bitmap(Tile.Class.fromTexture.Invoke(texture),scene);bitmap.blendMode=new BlendMode.AlphaAdd();}
   bitmap.visible=true;
  }
  if(bitmap!=null){if(Stopwatch.GetTimestamp()-ready>Stopwatch.Frequency/3||BitConverter.ToInt32(data,32)!=HostWorld.Instance.Generation){Hide();return;}bitmap.x=bitmap.y=0;bitmap.scaleX=scene.width/(double)ScreenChargeRules.Width;bitmap.scaleY=scene.height/(double)ScreenChargeRules.Height;}
 }
 public void Dispose(){bitmap?.remove();texture?.dispose();slot.Dispose();}
}
