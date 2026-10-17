using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using dc;
using dc.en;
namespace EarlyPlaytest;
// Native Hero temporarily owns DC interaction/menu flows; guest player logic is unchanged.
public sealed class PlayerMode:IDisposable {
 public static PlayerMode Instance;readonly Action<bool,string> change;readonly StreamWriter log;
 bool wanted=true,lease,escapeLease,alias,aliasHeld,disposed,f7Held;double began,released=-1,nextTrace;int stable;
 readonly HashSet<int> heldKeys=new HashSet<int>();
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr w,out uint pid);
 static double Now=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
 public PlayerMode(Action<bool,string> change){this.change=change;Instance=this;log=new StreamWriter(Path.Combine(EpSession.Output,"player_mode.csv"));log.AutoFlush=true;log.WriteLine("utc,qpc,event,generation,nativeRequested,heroUid,heroX,heroY,dy,gravity,hasGravity,weight,collision,manualLock,paused,focusedUid,result");dc.pr.Hook_Game.update+=GameUpdate;}
 static bool Foreground(){uint p;GetWindowThreadProcessId(GetForegroundWindow(),out p);return p==Process.GetCurrentProcess().Id;}
 static bool KeyDown(int code)=>code>=0&&dc.hxd.Key.Class.isDown.Invoke(code);
 static bool Pressed(int code)=>code>=0&&dc.hxd.Key.Class.isPressed.Invoke(code);
 bool Edge(int code){if(code<0)return false;bool down=KeyDown(code)||(GetAsyncKeyState(code)&0x8000)!=0;bool edge=down&&!heldKeys.Contains(code);if(down)heldKeys.Add(code);else heldKeys.Remove(code);return edge||Pressed(code);}
 static int[] InteractionKeys(Hero h){var b=h.controller.parent.get_bindings();var a=new[]{b.primary,b.secondary,b.third};int[] keys=new int[3];for(int i=0;i<3;i++)keys[i]=a[i]!=null&&a[i].length>6?Marshal.ReadInt32(a[i].bytes,6*4):-1;return keys;}
 void Write(string kind,string result){var h=HostWorld.Instance?.Hero;log.WriteLine(string.Join(",",DateTime.UtcNow.ToString("o"),Stopwatch.GetTimestamp(),kind,HostWorld.Instance?.Generation??0,HostWorld.Instance?.NativeRequested??false,h?.__uid??0,h==null?0:h.cx+h.xr,h==null?0:h.cy+h.yr,h?.dy??0,h?.gravity??0,h?.hasGravity??false,h?.baseWeight??0,h?.collisionMode?.GetType().Name??"",h?.controller.manualLock??false,h?._level?.game?.paused??false,h?.focused?.__uid??0,result.Replace(',',';')));}
 void Begin(bool pause,bool useAlias,int[] keys){lease=true;escapeLease=pause;alias=useAlias;aliasHeld=useAlias;stable=0;began=Now;released=-1;change(false,pause?"temporary Hero for native pause":"temporary Hero for native interaction");Write("lease_begin","pause="+pause+" enterAlias="+useAlias+" nativeInteractionAction=6 keys="+string.Join("/",keys));}
 static void GameUpdate(dc.pr.Hook_Game.orig_update orig,dc.pr.Game self){var i=Instance;if(i!=null&&!i.disposed)i.BeforeGame(self);orig(self);}
 void BeforeGame(dc.pr.Game game){var h=HostWorld.Instance?.Hero;if(h==null||h.destroyed||h._level?.game!=game||!Foreground()){heldKeys.Clear();return;}var keys=InteractionKeys(h);bool nativePress=false;foreach(int key in keys)nativePress|=Edge(key);bool search=Edge(13)&&Array.IndexOf(keys,13)<0;bool esc=Edge(27);
  if(wanted&&!lease&&HostWorld.Instance.NativeRequested&&(nativePress||search||esc)){Begin(esc,search&&!esc,keys);if(esc){bool minor=false;if(!game.paused&&!h.controlsLocked(ref minor)){new dc.ui.pause.DefaultPause();}Write("pause_request","native DefaultPause constructor with native control-lock check; actualPaused="+game.paused);}}
  // Enter is a short interaction alias. DC's configured interaction key keeps native long-press behavior.
  if(lease&&alias){if(KeyDown(13))aliasHeld=true;else if(aliasHeld){aliasHeld=false;alias=false;bool minor=false;if(!game.paused&&!game.hasCinematic()&&!h.controlsLocked(ref minor)){h.updateInteraction();int uid=h.focused?.__uid??0;h.activateFocus(false);Write("native_enter_activate","Native Hero.updateInteraction/activateFocus(false); targetUid="+uid);}else Write("alias_activation_deferred_to_native_ui","Paused/cinematic/locked; no forced world activation");}}
 }
 public void Frame(){if(disposed)return;bool fg=Foreground();bool f7=fg&&(GetAsyncKeyState(0x76)&0x8000)!=0;if(f7&&!f7Held){wanted=!HostWorld.Instance.NativeRequested;lease=false;alias=aliasHeld=false;released=-1;change(wanted,"manual F7");Write("manual_f7","requested="+wanted);}f7Held=f7;
  var h=HostWorld.Instance?.Hero;if(h==null||h.destroyed)return;HostWorld.Instance.RepairInactiveCarrier();double now=Now;if(now>=nextTrace){nextTrace=now+.1;Write("observe",(lease?"temporary Hero":"selected player")+" hp="+h.life+" awake="+h.awake+" cine="+h._level.game.hasCinematic());}
  if(!lease)return;var game=h._level.game;var keys=InteractionKeys(h);bool held=fg&&((escapeLease&&KeyDown(27))||(!escapeLease&&KeyDown(13)));foreach(int key in keys)held|=fg&&KeyDown(key);
  bool minor=false;bool locked=h.controlsLocked(ref minor)||h.controller.parent.isLocked||(h.controller.parent.exclusiveId!=null&&h.controller.parent.exclusiveId!=h.controller.id);
  bool busy=held||game.paused||game.hasCinematic()||locked;if(busy){released=-1;stable=0;return;}if(released<0)released=now;stable++;
  if(now-began>.125&&now-released>.1&&stable>=2){lease=false;alias=aliasHeld=false;change(wanted,"native interaction/menu finished");Write("lease_end","leaseMs="+((now-began)*1000).ToString("F2",CultureInfo.InvariantCulture)+" releasedMs="+((now-released)*1000).ToString("F2",CultureInfo.InvariantCulture)+"; Native pause/cinematic/controller locks clear; return="+(wanted?"ICEY":"HERO"));}
 }
 public void Dispose(){if(disposed)return;disposed=true;dc.pr.Hook_Game.update-=GameUpdate;log.Dispose();Instance=null;}
}
