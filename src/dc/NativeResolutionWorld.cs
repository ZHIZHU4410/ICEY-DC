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
  double fogFactor=self.fogFactor;double a=self.matA,b=self.matB,c=self.matC,d=self.matD,x=self.absX,y=self.absY,min=self.minScale;int width=self.maxWid,height=self.maxHei;
  // Remove both atmospheric fog layers only for this ICEY world draw.
  var cameraFog=self.level.lDisp?.camFog;bool visible=cameraFog?.visible??false;
  // Ground smoke is separate tiled room decoration, not the fog combine or combat FX.
  var groundSmoke=self.level.lDisp?.tgSmoke;bool smokeVisible=groundSmoke?.visible??false;
  // DC's HD world pass uses a much darker ambient minimum than the original pixel render.
  // Preserve native point lights and particles; fog intensity is a numeric parameter, not a shader variant.
  var ambient=self.ambientLight;double red=ambient==null?.65:Math.Max(.65,ambient.x),green=ambient==null?.65:Math.Max(.65,ambient.y),blue=ambient==null?.65:Math.Max(.65,ambient.z),alpha=ambient==null?1:ambient.w;
  var lifted=new Vector(HaxeProxy.Runtime.Ref<double>.From(ref red),HaxeProxy.Runtime.Ref<double>.From(ref green),HaxeProxy.Runtime.Ref<double>.From(ref blue),HaxeProxy.Runtime.Ref<double>.From(ref alpha));
  try{if(groundSmoke!=null)groundSmoke.visible=false;self.fogFactor=0;if(cameraFog!=null)cameraFog.visible=false;self.ambientLight=lifted;self.matA=self.matD=1;self.matB=self.matC=self.absX=self.absY=0;self.minScale=1;orig(self,context);}
  finally{if(groundSmoke!=null)groundSmoke.visible=smokeVisible;self.fogFactor=fogFactor;if(cameraFog!=null)cameraFog.visible=visible;self.ambientLight=ambient;self.matA=a;self.matB=b;self.matC=c;self.matD=d;self.absX=x;self.absY=y;self.minScale=min;self.maxWid=width;self.maxHei=height;}
 }
}
