using System;
using dc.h2d;
using dc.h3d;
namespace EarlyPlaytest;
// Preserve the native deferred passes and world layer order; only the render root uses window pixels.
public static class NativeResolutionWorld {
 static bool installed;
 public static void Install(){if(installed)return;installed=true;dc.light.Hook_LightedLayers.drawRec+=Draw;}
 public static void Dispose(){if(!installed)return;installed=false;dc.light.Hook_LightedLayers.drawRec-=Draw;}
 static void Draw(dc.light.Hook_LightedLayers.orig_drawRec orig,dc.light.LightedLayers self,RenderContext context){
  if(!CarrierGuard.Active||CarrierGuard.Hero?._level?.scroller!=self){orig(self,context);return;}
  double a=self.matA,b=self.matB,c=self.matC,d=self.matD,x=self.absX,y=self.absY,min=self.minScale;int width=self.maxWid,height=self.maxHei;
  // Exclude the broad screen fog film in ICEY mode; keep the brighter ambient floor.
  var cameraFog=self.level.lDisp?.camFog;bool visible=cameraFog?.visible??false;
  // DC's HD world pass uses a much darker ambient minimum than the original pixel render.
  // Lift only that floor for ICEY; native point lights, depth fog and effects still render.
  var ambient=self.ambientLight;double red=ambient==null?.65:Math.Max(.65,ambient.x),green=ambient==null?.65:Math.Max(.65,ambient.y),blue=ambient==null?.65:Math.Max(.65,ambient.z),alpha=ambient==null?1:ambient.w;
  var lifted=new Vector(HaxeProxy.Runtime.Ref<double>.From(ref red),HaxeProxy.Runtime.Ref<double>.From(ref green),HaxeProxy.Runtime.Ref<double>.From(ref blue),HaxeProxy.Runtime.Ref<double>.From(ref alpha));
  try{if(cameraFog!=null)cameraFog.visible=false;self.ambientLight=lifted;self.matA=self.matD=1;self.matB=self.matC=self.absX=self.absY=0;self.minScale=1;orig(self,context);}
  finally{if(cameraFog!=null)cameraFog.visible=visible;self.ambientLight=ambient;self.matA=a;self.matB=b;self.matC=c;self.matD=d;self.absX=x;self.absY=y;self.minScale=min;self.maxWid=width;self.maxHei=height;}
 }
}
