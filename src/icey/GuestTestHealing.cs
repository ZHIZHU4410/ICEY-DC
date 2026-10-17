using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
namespace EarlyPlaytest {
public static class GuestTestHealing {
 static readonly HashSet<long> applied=new HashSet<long>();static int generation=-1;static float nextFixture;static string fixture="";
 public static bool Handle(string[] p){if(p.Length<2||p[1]!="TESTHEAL")return false;int g;long id;if(p.Length!=6||p[2]!=GuestCombat.Contract||!int.TryParse(p[3],out g)||!long.TryParse(p[4],out id)){InputAction.Instance.Event("TEST_HEAL_REJECTED shape");return true;}
  string result="";int before=0,after=0,max=0;
  if(!BridgeKeyboard.NativeMode||!BridgeArena.Active||WorldMirror.Instance==null||!WorldMirror.Instance.Active||g!=WorldMirror.Instance.Generation||R.Player.gameObject==null)result="rejected_mode_or_generation";
  else{var attr=R.Player.Attribute;before=after=attr.currentHP;max=attr.maxHP;if(generation!=g){generation=g;applied.Clear();}
   if(!applied.Add(id))result="duplicate_ignored";else if(before<=0||max<=0)result="rejected_dead";else if(before==max)result="already_full";
   else{R.Player.Action.HPRecover(max-before);after=attr.currentHP;result=after==max?"native_HPRecover_full":"FAIL_native_HPRecover";}
  }
  long qpc=Stopwatch.GetTimestamp();InputAction.Instance.Event("TEST_HEAL generation="+g+" id="+id+" result="+result+" hp="+before+"->"+after+" max="+max+" tick="+InputAction.Instance.PhysicsTick+" frame="+CpuFrames.LastReadyFrameId);
  InputAction.Instance.wire.Send(string.Join("|",new string[]{"EP4","HEALED",GuestCombat.Contract,g.ToString(),id.ToString(),result,before.ToString(),after.ToString(),max.ToString(),InputAction.Instance.PhysicsTick.ToString(),qpc.ToString(),CpuFrames.LastReadyFrameId.ToString()}));return true;
 }
 // Explicit local fixture, absent from manual sessions. This is not a combat-damage acceptance test.
 public static void ProbeFixture(){if(Time.realtimeSinceStartup<nextFixture)return;nextFixture=Time.realtimeSinceStartup+.2f;if(!BridgeArena.Active||!BridgeKeyboard.NativeMode||R.Player.gameObject==null)return;string path=Path.Combine(InputAction.Out,"healing_probe.txt");if(!File.Exists(path)||File.Exists(Path.Combine(InputAction.Out,"combat.flag")))return;
  string value=File.ReadAllText(path).Trim();if(value==fixture)return;var p=value.Split('|');int target;if(p.Length!=2||!int.TryParse(p[1],out target)||target<1||target>R.Player.Attribute.maxHP)throw new InvalidOperationException("Invalid explicit healing probe");fixture=value;int before=R.Player.Attribute.currentHP;R.Player.Action.HPRecover(target-before);InputAction.Instance.Event("TEST_HEAL_FIXTURE runtimeOnly hp="+before+"->"+R.Player.Attribute.currentHP+" id="+p[0]+" no combat claim; saves guarded");
 }
}
}
