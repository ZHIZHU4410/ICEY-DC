using System;
using dc.h2d;
namespace EarlyPlaytest;
// Preserve the native deferred passes and world layer order; only the render root uses window pixels.
public static class NativeResolutionWorld {
 static bool installed;
 public static void Install(){if(installed)return;installed=true;dc.light.Hook_LightedLayers.drawRec+=Draw;}
 public static void Dispose(){if(!installed)return;installed=false;dc.light.Hook_LightedLayers.drawRec-=Draw;}
 static void Draw(dc.light.Hook_LightedLayers.orig_drawRec orig,dc.light.LightedLayers self,RenderContext context){
  if(!CarrierGuard.Active||CarrierGuard.Hero?._level?.scroller!=self){orig(self,context);return;}
  double a=self.matA,b=self.matB,c=self.matC,d=self.matD,x=self.absX,y=self.absY,min=self.minScale;int width=self.maxWid,height=self.maxHei;
  // The broad additive CamFog film uses screen/world-dependent noise in the HD pass.
  // Suppress this decoration for ICEY, retaining native depth fog, lighting and actual smoke/FX.
  var cameraFog=self.level.lDisp?.camFog;bool visible=cameraFog?.visible??false;
  try{if(cameraFog!=null)cameraFog.visible=false;self.matA=self.matD=1;self.matB=self.matC=self.absX=self.absY=0;self.minScale=1;orig(self,context);}
  finally{if(cameraFog!=null)cameraFog.visible=visible;self.matA=a;self.matB=b;self.matC=c;self.matD=d;self.absX=x;self.absY=y;self.minScale=min;self.maxWid=width;self.maxHei=height;}
 }
}
