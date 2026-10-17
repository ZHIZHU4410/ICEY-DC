using System;
using System.Diagnostics;
using System.IO;
using dc.en.ltrap;
namespace EarlyPlaytest;
// Test setup only: native constructor+init; native touch/attack/cooldown/dispose remain in DC.
public sealed class TrapFixture:IDisposable {
 readonly string output;Spikes spike;string request="";double next;int generation;
 public TrapFixture(string output){this.output=output;}
 public void Update(){if(!CarrierGuard.Active)return;double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;if(now<next)return;next=now+.1;string file=Path.Combine(output,"trap_request.txt");if(!File.Exists(file))return;string value=File.ReadAllText(file).Trim();if(value==request)return;request=value;var p=value.Split('|');if(p.Length!=2)return;
  if(p[1]=="remove"){Remove("Explicit fixture cleanup");return;}if(p[1]!="spawn"||File.Exists(Path.Combine(output,"defense.flag"))){HostCombat.Instance.Write("trap_fixture_rejected",0,0,0,0,"Require spawn while defense gate closed");return;}
  if(spike!=null&&!spike.destroyed){HostCombat.Instance.Write("trap_fixture_rejected",0,spike.__uid,0,0,"Previous fixture still exists");return;}var h=CarrierGuard.Hero;var list=h._level.entities;for(int i=0;i<list.length;i++){var e=list.getDyn(i) as Spikes;if(e!=null&&!e.destroyed&&e.cx==h.cx&&e.cy==h.cy){HostCombat.Instance.Write("trap_fixture_rejected",0,e.__uid,h.__uid,0,"Existing native spikes at requested cell");return;}}
  spike=new Spikes(h._level,h.cx,h.cy,0,9,false,false,null);spike.init();generation=HostWorld.Instance.Generation;HostCombat.Instance.Write("trap_fixture_spawn",0,spike.__uid,h.__uid,0,"Native Spikes constructor+init; generation="+generation+" x="+(spike.cx+spike.xr)+" y="+(spike.cy+spike.yr)+" HeroX="+(h.cx+h.xr)+" HeroY="+(h.cy+h.yr)+" touch="+spike.hasEntityTouchChecks);
 }
 void Remove(string reason){if(spike==null||spike.destroyed)return;int uid=spike.__uid;spike.destroy();HostCombat.Instance?.Write("trap_fixture_removed",0,uid,0,0,reason+"; native destroy/dispose removes its map spot");spike=null;}
 public void Dispose(){Remove("Bridge shutdown");}
}
