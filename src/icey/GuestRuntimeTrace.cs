using System;
using System.Diagnostics;
using System.IO;
using HarmonyLib;
using UnityEngine;
namespace EarlyPlaytest {
public sealed class GuestRuntimeTrace:MonoBehaviour {
 StreamWriter log;WorldTime clock;float next;bool have,invincible,flash;static readonly System.Reflection.FieldInfo flashFrames=AccessTools.Field(typeof(PlayerAnimEventListener),"_flashEndCount");
 void Start(){log=new StreamWriter(Path.Combine(InputAction.Out,"native_time.csv"));log.AutoFlush=true;log.WriteLine("utc,qpc,frameId,tickId,generation,timeScale,fixedDeltaTime,worldFps,clockScene,clockEnabled,clockFrozen,clockSlow,pose,invincible,flash,flashFrames");}
 void LateUpdate(){if(R.Player.gameObject==null||!BridgeArena.Active)return;if(clock==null){clock=FindObjectOfType<WorldTime>();if(clock!=null){clock.FrozenEvent+=Frozen;clock.ResumeEvent+=Resumed;InputAction.Instance.Event("NATIVE_CLOCK_FOUND scene="+clock.gameObject.scene.name+" enabled="+clock.enabled+" fps="+WorldTime.Fps);}}var p=R.Player.gameObject.GetComponent<PlayerAbilities>();bool i=p.hurt.Invincible,f=R.Player.Attribute.flashFlag;var listener=R.Player.gameObject.GetComponent<PlayerAnimEventListener>();int remaining=(int)flashFrames.GetValue(listener);
 if(!have||i!=invincible||f!=flash){have=true;invincible=i;flash=f;GuestCombat.Instance?.Write("native_defense_flags",InputAction.Instance.Sequence,0,"Invincible="+i+" flashFlag="+f+" flashFrames="+remaining+" worldFps="+WorldTime.Fps+" timeScale="+Time.timeScale);}
 if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.1f;log.WriteLine(string.Join(",",new string[]{DateTime.UtcNow.ToString("o"),Stopwatch.GetTimestamp().ToString(),CpuFrames.LastReadyFrameId.ToString(),InputAction.Instance.PhysicsTick.ToString(),WorldMirror.Instance.Generation.ToString(),Time.timeScale.ToString("R",System.Globalization.CultureInfo.InvariantCulture),Time.fixedDeltaTime.ToString("R",System.Globalization.CultureInfo.InvariantCulture),WorldTime.Fps.ToString("R",System.Globalization.CultureInfo.InvariantCulture),clock==null?"missing":clock.gameObject.scene.name,(clock!=null&&clock.enabled).ToString(),(clock!=null&&clock.IsFrozen).ToString(),(clock!=null&&clock.IsSlow).ToString(),R.Player.Action.stateMachine.currentState,i.ToString(),f.ToString(),remaining.ToString()}));}
 void Frozen(object sender,WorldTime.FrozenArgs args){GuestCombat.Instance?.Write("native_time_frozen",0,args.Target==null?0:args.Target.GetComponent<CombatIdentity>()?.uid??0,"scope="+args.Type+" timeScale="+Time.timeScale);}
 void Resumed(object sender,WorldTime.FrozenArgs args){GuestCombat.Instance?.Write("native_time_resumed",0,args.Target==null?0:args.Target.GetComponent<CombatIdentity>()?.uid??0,"scope="+args.Type+" timeScale="+Time.timeScale);}
 void OnDestroy(){if(clock!=null){clock.FrozenEvent-=Frozen;clock.ResumeEvent-=Resumed;}log?.Dispose();}
}
[HarmonyPatch(typeof(PlayerJumpAbility),"Jump")]static class NativeRecoveryTrace {
 [HarmonyPrefix]static void Before(){if(!BridgeArena.Active)return;var listener=R.Player.gameObject.GetComponent<PlayerAnimEventListener>();GuestCombat.Instance?.Write("native_jump_callback_enter",InputAction.Instance.Sequence,0,"hitJump="+listener.hitJump+" Invincible="+R.Player.gameObject.GetComponent<PlayerAbilities>().hurt.Invincible);}
 [HarmonyPostfix]static void After(){if(!BridgeArena.Active)return;GuestCombat.Instance?.Write("native_jump_callback_return",InputAction.Instance.Sequence,0,"Invincible="+R.Player.gameObject.GetComponent<PlayerAbilities>().hurt.Invincible);}
}
}
