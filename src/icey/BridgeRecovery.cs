using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
namespace EarlyPlaytest {
// Prevent native death scene teardown only for the active transient bridge player.
static class BridgeRecovery {
 public static bool Revive(){if(!BridgeArena.Active||!BridgeKeyboard.NativeMode||R.Player.gameObject==null||!R.Player.Attribute.isDead)return false;
  var a=R.Player.Attribute;a.currentHP=a.maxHP;var hurt=R.Player.gameObject.GetComponent<PlayerAbilities>().hurt;hurt.DeadFlag=false;
  R.Player.TimeController.SetSpeed(Vector2.zero);GuestCombat.Instance?.Write("bridge_respawn_full_hp",0,0,"same player/position/world generation; death scene suppressed");return true;
 }
}
[HarmonyPatch(typeof(PlayerHurtAbility),"UpdatePlayerDeath")]
static class BridgeDeathRecovery {[HarmonyPrefix]static void Before(){BridgeRecovery.Revive();}}
[HarmonyPatch(typeof(PlayerHurtAbility),"HpMinus")]
static class BridgeLethalRecovery {[HarmonyPostfix]static void After(){BridgeRecovery.Revive();}}
// Hide camera-sized native color masks only while the transparent export camera renders.
// They belong to ICEY's screen composition, not to the guest character/effects layer.
public sealed class CaptureMaskGuard:MonoBehaviour {
 readonly List<Renderer> hidden=new List<Renderer>();
 float nextTrace;
 void OnPreCull(){Restore();if(!BridgeArena.Active)return;
  foreach(var mask in UnityEngine.Object.FindObjectsOfType<FullScreenColorMaskController>())foreach(var renderer in mask.GetComponentsInChildren<Renderer>(true))if(renderer.enabled){renderer.enabled=false;hidden.Add(renderer);}
  foreach(var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>()){
   if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
   // The charge prefab also contains real animated slash/light sprites. Keep those.
   // Only its opaque screen-sized Black/White backdrops must stay out of the RGBA export.
   // The separate effect-only capture now exports this whole animation to the viewport.
   // Hide it only from the player/world capture to avoid drawing the same slash twice.
   for(Transform t=renderer.transform;t!=null;t=t.parent)if(t.name.StartsWith("ChargingScreenEffect")){renderer.enabled=false;hidden.Add(renderer);break;}
  }
  if(File.Exists(Path.Combine(InputAction.Out,"capture_trace.flag"))&&Time.realtimeSinceStartup>nextTrace){nextTrace=Time.realtimeSinceStartup+.1f;foreach(var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())if(renderer.enabled&&renderer.gameObject.activeInHierarchy&&!renderer.transform.IsChildOf(R.Player.transform))InputAction.Instance.Event("CAPTURE_RENDERER "+renderer.name+" layer="+renderer.gameObject.layer+" bounds="+renderer.bounds+" shader="+(renderer.sharedMaterial==null?"null":renderer.sharedMaterial.shader.name));}
 }
 void OnPostRender(){Restore();}
 void OnDisable(){Restore();}
 void Restore(){foreach(var renderer in hidden)if(renderer!=null)renderer.enabled=true;hidden.Clear();}
}
// Opt-in runtime probes. Manual sessions never create this command file.
public sealed class BridgeRegression:MonoBehaviour {
 string last="";float next;
 void Update(){if(Time.realtimeSinceStartup<next||!BridgeArena.Active||!BridgeKeyboard.NativeMode)return;next=Time.realtimeSinceStartup+.2f;
  string path=Path.Combine(InputAction.Out,"v105_probe.txt");if(!File.Exists(path))return;string command=File.ReadAllText(path).Trim();if(command==last)return;last=command;
  try{string verb=command.Split('|')[0];
   if(verb=="lethal"){var hurt=R.Player.gameObject.GetComponent<PlayerAbilities>().hurt;AccessTools.Method(typeof(PlayerHurtAbility),"HpMinus").Invoke(hurt,new object[]{R.Player.Attribute.maxHP+1});}
   if(verb=="chips")AccessTools.Method(typeof(GuestCombat),"SpawnExecuteChips").Invoke(GuestCombat.Instance,new object[]{-105,(Vector2)R.Player.transform.position});
   if(verb=="mask")R.Effect.Generate(213);
   InputAction.Instance.Event("V105_PROBE "+command+" hp="+R.Player.Attribute.currentHP+" chips="+ChipManager.HasChipInScene());
   foreach(var entry in R.Effect.fxData)if(entry.Value!=null&&entry.Value.effect!=null&&(entry.Value.effect.GetComponent<EnemyChipMove>()!=null||entry.Value.effect.name.Contains("Black")))InputAction.Instance.Event("V105_EFFECT "+entry.Key+" "+entry.Value.effect.name);
  }catch(Exception e){InputAction.Instance.Event("V105_PROBE_FAIL "+e);}
 }
}
}
