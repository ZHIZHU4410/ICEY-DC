using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
namespace EarlyPlaytest {
// A separate native-effect-only frame. Stretching this frame never changes player/world scale.
public sealed class ScreenChargeCapture:MonoBehaviour {
 FrameSlot slot;byte[] packet=new byte[ScreenChargeRules.Capacity];Camera camera;RenderTexture rt;Texture2D readback;CommandBuffer commands;long frame;bool visible,stopped;float next,nextControl;StreamWriter log;
 void Start(){slot=new FrameSlot(ScreenChargeRules.Mailbox,ScreenChargeRules.Capacity);log=new StreamWriter(Path.Combine(InputAction.Out,"screen_charge_capture.csv"));log.AutoFlush=true;log.WriteLine("utc,frameId,generation,visible,renderers,captureMs");StartCoroutine(Capture());}
 public static Transform Root(Renderer renderer){Transform root=null;for(var t=renderer.transform;t!=null;t=t.parent)if(t.name.StartsWith("ChargingScreenEffect"))root=t;return root;}
 public static bool Backdrop(Renderer renderer){return renderer is SpriteRenderer&&(renderer.name=="Black"||renderer.name=="White");}
 IEnumerator Capture(){while(!stopped){yield return new WaitForEndOfFrame();if(stopped)yield break;if(Time.realtimeSinceStartup<next)continue;next=Time.realtimeSinceStartup+1f/30;
  try{if(Time.realtimeSinceStartup>nextControl){nextControl=Time.realtimeSinceStartup+.25f;if(File.ReadAllText(Path.Combine(InputAction.Out,"control.txt")).Trim()=="stop"){Dispose();yield break;}}
   bool active=BridgeArena.Active&&BridgeKeyboard.NativeMode&&WorldMirror.Instance!=null&&WorldMirror.Instance.Active;
   var renderers=new List<Renderer>();Transform root=null;
   if(active)foreach(var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>()){if(!renderer.enabled||!renderer.gameObject.activeInHierarchy||Backdrop(renderer))continue;var candidate=Root(renderer);if(candidate!=null){renderers.Add(renderer);root=candidate;}}
   if(renderers.Count==0){if(visible)Publish(false,0,0);continue;}
   long start=Stopwatch.GetTimestamp();Prepare();commands.Clear();commands.SetRenderTarget(rt);commands.ClearRenderTarget(true,true,Color.clear);
   foreach(var renderer in renderers){var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)if(materials[i]!=null)commands.DrawRenderer(renderer,materials[i],i);}
   var original=Camera.main;camera.orthographicSize=original==null?6f:Mathf.Max(4f,original.orthographicSize);camera.aspect=ScreenChargeRules.Width/(float)ScreenChargeRules.Height;camera.transform.position=new Vector3(root.position.x,root.position.y,root.position.z-20);camera.transform.rotation=Quaternion.identity;
   camera.Render();var previous=RenderTexture.active;
   try{RenderTexture.active=rt;readback.ReadPixels(new Rect(0,0,ScreenChargeRules.Width,ScreenChargeRules.Height),0,0,false);var colors=readback.GetPixels32();int width=ScreenChargeRules.Width,height=ScreenChargeRules.Height;
    for(int y=0;y<height;y++)for(int x=0;x<width;x++){Color32 color=colors[(height-1-y)*width+x];int o=FrameSlot.Header+(y*width+x)*4;packet[o]=color.r;packet[o+1]=color.g;packet[o+2]=color.b;packet[o+3]=color.a;}
   }finally{RenderTexture.active=previous;}
   Publish(true,renderers.Count,(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency);
  }catch(Exception e){InputAction.Instance.Event("SCREEN_CHARGE_CAPTURE_FAIL "+e);Dispose();}
 }}
 void Prepare(){if(camera!=null)return;var go=new GameObject("BridgeScreenChargeCapture");DontDestroyOnLoad(go);camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=0;camera.nearClipPlane=.1f;camera.farClipPlane=100;
  rt=new RenderTexture(ScreenChargeRules.Width,ScreenChargeRules.Height,24,RenderTextureFormat.ARGB32);if(!rt.Create())throw new InvalidOperationException("Charge RT creation failed");camera.targetTexture=rt;readback=new Texture2D(ScreenChargeRules.Width,ScreenChargeRules.Height,TextureFormat.ARGB32,false);commands=new CommandBuffer();camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,commands);
 }
 void Publish(bool show,int count,double ms){visible=show;FrameSlot.I(packet,0,0x34504645);FrameSlot.I(packet,4,4);FrameSlot.L(packet,8,++frame);FrameSlot.I(packet,32,WorldMirror.Instance==null?0:WorldMirror.Instance.Generation);FrameSlot.I(packet,36,ScreenChargeRules.Width);FrameSlot.I(packet,40,ScreenChargeRules.Height);FrameSlot.L(packet,48,Stopwatch.GetTimestamp());FrameSlot.I(packet,56,show?1:0);slot.Write(packet);log.WriteLine(string.Join(",",new string[]{DateTime.UtcNow.ToString("o"),frame.ToString(),(WorldMirror.Instance==null?0:WorldMirror.Instance.Generation).ToString(),show.ToString(),count.ToString(),ms.ToString(System.Globalization.CultureInfo.InvariantCulture)}));}
 void Dispose(){if(stopped)return;if(visible)Publish(false,0,0);stopped=true;slot?.Dispose();log?.Dispose();commands?.Dispose();if(camera!=null)Destroy(camera.gameObject);if(rt!=null){rt.Release();Destroy(rt);}if(readback!=null)Destroy(readback);}
 void OnDestroy(){Dispose();}
}
}
