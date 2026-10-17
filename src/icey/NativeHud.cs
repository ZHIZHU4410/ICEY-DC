using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
namespace EarlyPlaytest {
// Capture the live native combat widgets, without the original level or menus.
public sealed class NativeHud:MonoBehaviour {
 [DllImport("ep5_hud.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]static extern int EP4_Configure(IntPtr texture,string name);
 [DllImport("ep5_hud.dll",CallingConvention=CallingConvention.Cdecl)]static extern int EP4_Submit(int id,IntPtr header);
 [DllImport("ep5_hud.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr EP4_RenderEvent();
 [DllImport("ep5_hud.dll",CallingConvention=CallingConvention.Cdecl)]static extern int EP4_Error();
 [DllImport("ep5_hud.dll",CallingConvention=CallingConvention.Cdecl)]static extern void EP4_Stop();
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibraryW(string path);
 const int W=512,H=128;Camera camera;RenderTexture rt;IntPtr renderEvent;GCHandle pinned;FrameSlot slot;StreamWriter log;long id,observed;bool stopped;float next;
 readonly byte[] packet=new byte[FrameSlot.Size],read=new byte[FrameSlot.Size];
 void Start(){slot=new FrameSlot(@"Local\IceyDCBridge_FP50_HUD");pinned=GCHandle.Alloc(packet,GCHandleType.Pinned);log=new StreamWriter(Path.Combine(InputAction.Out,"native_hud.csv"));log.AutoFlush=true;log.WriteLine("utc,frameId,playerFrameId,tickId,generation,hp,maxHp,energy,maxEnergy,submitQpc,readyQpc,zeroAlpha,partialAlpha,hash");StartCoroutine(Capture());}
 IEnumerator Capture(){while(!stopped){yield return new WaitForEndOfFrame();if(stopped||!BridgeArena.Active||Time.realtimeSinceStartup<next)continue;next=Time.realtimeSinceStartup+.05f;try{if(!Prepare())continue;camera.Render();var input=InputAction.Instance;long start=Stopwatch.GetTimestamp();id++;FrameSlot.I(packet,0,0x34504645);FrameSlot.I(packet,4,4);FrameSlot.L(packet,8,id);FrameSlot.L(packet,16,input.PhysicsTick);FrameSlot.L(packet,24,CpuFrames.LastReadyFrameId);FrameSlot.I(packet,32,WorldMirror.Instance.Generation);FrameSlot.I(packet,36,W);FrameSlot.I(packet,40,H);FrameSlot.I(packet,44,W*H*4);FrameSlot.L(packet,72,start);FrameSlot.I(packet,96,R.Player.Attribute.currentHP);FrameSlot.I(packet,100,R.Player.Attribute.maxHP);FrameSlot.I(packet,104,R.Player.Attribute.currentEnergy);FrameSlot.I(packet,108,R.Player.Attribute.maxEnergy);
 if(EP4_Error()!=0)throw new InvalidOperationException("HUD staging error "+EP4_Error());if(EP4_Submit((int)id,pinned.AddrOfPinnedObject())!=0)GL.IssuePluginEvent(renderEvent,(int)id);
 if(slot.Read(read,observed)){observed=BitConverter.ToInt64(read,8);log.WriteLine(string.Join(",",new string[]{DateTime.UtcNow.ToString("o"),observed.ToString(),BitConverter.ToInt64(read,24).ToString(),BitConverter.ToInt64(read,16).ToString(),BitConverter.ToInt32(read,32).ToString(),BitConverter.ToInt32(read,96).ToString(),BitConverter.ToInt32(read,100).ToString(),BitConverter.ToInt32(read,104).ToString(),BitConverter.ToInt32(read,108).ToString(),BitConverter.ToInt64(read,72).ToString(),BitConverter.ToInt64(read,48).ToString(),BitConverter.ToInt32(read,272).ToString(),BitConverter.ToInt32(read,276).ToString(),BitConverter.ToUInt32(read,180).ToString()}));}
 }catch(Exception e){InputAction.Instance.Event("HUD_FAIL "+e);Dispose();}}}
 bool Prepare(){if(camera!=null)return true;var bind=FindObjectOfType<UIDataBind>();if(bind==null||R.Ui.Camera==null)return false;var original=R.Ui.Camera.GetComponentInChildren<Camera>();if(original==null)throw new InvalidOperationException("Native UI camera not found");Bounds bounds=new Bounds();bool found=false;
 string[] fields={"_hpBgSprite","_hp1Sprite","_hp2Sprite","_roleNameLabel"};foreach(string name in fields){var widget=AccessTools.Field(typeof(UIDataBind),name).GetValue(bind) as UIWidget;if(widget!=null)Extend(widget,ref bounds,ref found);}
 var energy=AccessTools.Field(typeof(UIDataBind),"energyController").GetValue(bind) as UIEnergyController;if(energy!=null)foreach(UIWidget widget in energy.GetComponentsInChildren<UIWidget>(true))Extend(widget,ref bounds,ref found);
 if(!found)throw new InvalidOperationException("Native HP/shield widget bounds empty");if(LoadLibraryW(Path.Combine(BepInEx.Paths.GameRootPath,"ep5_hud.dll"))==IntPtr.Zero)throw new InvalidOperationException("HUD CPU staging DLL unavailable");var go=new GameObject("BridgeNativeCombatHud");DontDestroyOnLoad(go);camera=go.AddComponent<Camera>();camera.CopyFrom(original);camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.rect=new Rect(0,0,1,1);camera.orthographic=true;camera.aspect=W/(float)H;camera.orthographicSize=Mathf.Max(bounds.extents.y*1.12f,bounds.extents.x/camera.aspect*1.08f);camera.transform.rotation=original.transform.rotation;camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,original.transform.position.z);rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;int format=EP4_Configure(rt.GetNativeTexturePtr(),@"Local\IceyDCBridge_FP50_HUD");if(format<0)throw new InvalidOperationException("HUD configure "+format);renderEvent=EP4_RenderEvent();InputAction.Instance.Event("HUD_CAPTURE native live HP+energy widgets bounds="+bounds+" camera="+original.name+" mask="+camera.cullingMask+" format="+format+" resolution=512x128 CPU 20Hz");return true;
 }
 static void Extend(UIWidget widget,ref Bounds bounds,ref bool found){foreach(Vector3 point in widget.worldCorners){if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);}}
 void Dispose(){if(stopped)return;stopped=true;EP4_Stop();if(renderEvent!=IntPtr.Zero)GL.IssuePluginEvent(renderEvent,-1);if(pinned.IsAllocated)pinned.Free();slot?.Dispose();log?.Dispose();if(camera!=null)Destroy(camera.gameObject);if(rt!=null){rt.Release();Destroy(rt);}}
 void OnDestroy(){Dispose();}
}
}
