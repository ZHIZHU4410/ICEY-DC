using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
namespace EarlyPlaytest;
public sealed class TestHealing:IDisposable {
 public static TestHealing Instance;readonly StreamWriter log;bool held;long id;readonly Dictionary<long,Tuple<int,long>> pending=new();
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr w,out uint pid);
 public TestHealing(){Instance=this;log=new StreamWriter(Path.Combine(EpSession.Output,"test_heal_host.csv"));log.AutoFlush=true;log.WriteLine("utc,qpc,event,generation,eventId,frameId,tickId,result");}
 void Write(string type,int gen,long eventId,string result){log.WriteLine(string.Join(",",DateTime.UtcNow.ToString("o"),Stopwatch.GetTimestamp(),type,gen,eventId,CpuDisplay.Instance?.CurrentFrameId??0,CpuDisplay.Instance?.CurrentTickId??0,result.Replace(',',';')));}
 public void Frame(GateWire wire){uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);bool down=pid==Process.GetCurrentProcess().Id&&(GetAsyncKeyState(0x77)&0x8000)!=0;bool edge=down&&!held;held=down;
  long now=Stopwatch.GetTimestamp();var expired=new List<long>();foreach(var p in pending)if(now-p.Value.Item2>Stopwatch.Frequency*2)expired.Add(p.Key);foreach(long p in expired){Write("FAIL_ack_timeout",pending[p].Item1,p,"2s; no automatic replay");pending.Remove(p);}
  if(!edge)return;int gen=HostWorld.Instance?.Generation??0;
  if(!CarrierGuard.Active||!wire.Connected||HostWorld.Instance==null||!HostWorld.Instance.NativeRequested||CarrierGuard.Hero._level.game.paused){Write("rejected_mode",gen,0,"F8 only in active unpaused ICEY mode");return;}
  long eventId=++id;pending[eventId]=Tuple.Create(gen,now);wire.Send(string.Join("|","EP4","TESTHEAL",HostCombat.Contract,gen,eventId,now));Write("requested",gen,eventId,"F8 full native HP; no potion/energy resource consumed");
 }
 public bool Handle(string[] p){if(p.Length<2||p[1]!="HEALED")return false;if(p.Length!=12||p[2]!=HostCombat.Contract||!int.TryParse(p[3],out int gen)||!long.TryParse(p[4],out long eventId)){Write("rejected_shape",0,0,string.Join("|",p));return true;}
  if(!pending.TryGetValue(eventId,out var request)||gen!=request.Item1||gen!=HostWorld.Instance.Generation){Write("rejected_stale_ack",gen,eventId,string.Join("|",p));return true;}
  pending.Remove(eventId);Write("native_result",gen,eventId,string.Join("|",p)+" latencyMs="+((Stopwatch.GetTimestamp()-request.Item2)*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));return true;
 }
 public void Dispose(){log.Dispose();Instance=null;}
}
