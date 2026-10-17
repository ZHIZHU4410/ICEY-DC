using System;
using System.Diagnostics;
using System.IO;
using dc;
using dc.en;
using dc.en.inter;
using InventItem=dc.tool.InventItem;
namespace EarlyPlaytest;
// Read-only instrumentation of existing level objects. Never constructs an entity.
public sealed class InteractionProbe:IDisposable {
 readonly StreamWriter log;double next;
 public InteractionProbe(){log=new StreamWriter(Path.Combine(EpSession.Output,"nearby_interactables.csv"));log.AutoFlush=true;log.WriteLine("utc,qpc,generation,uid,type,x,y,heroX,heroY,distanceCells,canActivate");Hook_Door.onActivate+=DoorActivate;Hook_Hero.activateFocus+=Focus;Hook_Hero.onUseInteractive+=Use;Hook_Hero.onAfterPickItem+=Picked;}
 public void Update(){if(!File.Exists(Path.Combine(EpSession.Output,"inspect_interactables.flag")))return;double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;if(now<next)return;next=now+.5;var h=HostWorld.Instance?.Hero;if(h==null||h.destroyed)return;var all=h._level.entities;for(int i=0;i<all.length;i++){var e=all.getDyn(i) as Entity;if(e==null||e.destroyed||e==h)continue;double x=e.cx+e.xr,y=e.cy+e.yr,d=System.Math.Sqrt(System.Math.Pow(x-h.cx-h.xr,2)+System.Math.Pow(y-h.cy-h.yr,2));if(d>20)continue;bool can=e.canBeActivated(h);if(!can&&e is not Interactive)continue;log.WriteLine(string.Join(",",DateTime.UtcNow.ToString("o"),Stopwatch.GetTimestamp(),HostWorld.Instance.Generation,e.__uid,e.GetType().FullName,x,y,h.cx+h.xr,h.cy+h.yr,d,can));}}
 static void Focus(Hook_Hero.orig_activateFocus orig,Hero self,bool longPress){var e=self.focused;HostCombat.Instance?.Write("native_activate_focus_enter",0,self.__uid,e?.__uid??0,0,"targetType="+e?.GetType().FullName+" long="+longPress);orig(self,longPress);HostCombat.Instance?.Write("native_activate_focus_return",0,self.__uid,e?.__uid??0,0,"targetDestroyed="+(e?.destroyed??false)+" actualPaused="+self._level.game.paused+"; native world handler returned");}
 static void DoorActivate(Hook_Door.orig_onActivate orig,Door self,Hero by,bool longPress){bool before=self._closed;orig(self,by,longPress);HostCombat.Instance?.Write("native_door_onActivate",0,by.__uid,self.__uid,0,"closedBefore="+before+" closedAfter="+self._closed+"; existing level door");}
 static void Use(Hook_Hero.orig_onUseInteractive orig,Hero self,Interactive e){orig(self,e);HostCombat.Instance?.Write("native_world_interactive_used",0,self.__uid,e.__uid,0,e.GetType().FullName);}
 static void Picked(Hook_Hero.orig_onAfterPickItem orig,Hero self,InventItem item){orig(self,item);HostCombat.Instance?.Write("native_world_item_picked",0,self.__uid,0,0,"Native onAfterPickItem; inventory changed by DC");}
 public void Dispose(){Hook_Door.onActivate-=DoorActivate;Hook_Hero.activateFocus-=Focus;Hook_Hero.onUseInteractive-=Use;Hook_Hero.onAfterPickItem-=Picked;log.Dispose();}
}
