using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ModCore.Events.Interfaces.Game;
using ModCore.Mods;

namespace EarlyPlaytest;
public sealed class DcInput(ModInfo info):ModBase(info),IOnFrameUpdate,IOnGameExit,IOnBeforeGameInit
{
    static readonly string Out=EpSession.Output;
    TestHealing healing;DashDisplay dash;InteractionProbe interactionProbe;PlayerMode playerMode;TrapFixture trapFixture;HudDisplay hud;HostCombat combat;CarrierGuard guard;HostWorld world;CpuDisplay display; GateWire wire;StreamWriter log;int pid;long sequence;bool button;float axis;int combatMask,lastPcMask;float combatVertical;bool enabled,stopped;long modeEpoch;double nextMode;
    double elapsed,lastControl;string phase="setup";
    [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr w,out uint pid);
    public override void Initialize(){Directory.CreateDirectory(Out);string core=typeof(ModBase).Assembly.GetName().Version.ToString();if(core!="35.13.8.0"){File.WriteAllText(Path.Combine(Out,"dc_version_mismatch.txt"),DateTime.UtcNow.ToString("o")+" expected DCCM 35.13.8.0; actual "+core+"; install tested version or rerun compatibility gates");Logger.Error("[EP4D] DCCM version mismatch {Version}; input diagnostic disabled",core);stopped=true;return;}log=new StreamWriter(Path.Combine(Out,"dc_input_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+".csv"),false);log.WriteLine("utc,ticks,frequency,sequence,phase,button,axis,foregroundPid,dcForeground,hxdJ,hxdI,hxdK,combatMask,vertical,pcMask");pid=Process.GetCurrentProcess().Id;guard=new CarrierGuard(Out);combat=new HostCombat(Out);world=new HostWorld(Out);display=new CpuDisplay(Logger);hud=new HudDisplay();dash=new DashDisplay();healing=new TestHealing();trapFixture=new TrapFixture(Out);CpuDisplay.InstallHooks();wire=new GateWire("DC");wire.Start();playerMode=new PlayerMode(ChangePlayer);interactionProbe=new InteractionProbe();Logger.Information("[EP4D] input action diagnostic loaded pid={Pid}; Native combat input forwarding; Hero combat quarantined until combat.flag",pid);}
    void IOnFrameUpdate.OnFrameUpdate(double dt){if(stopped)return;try{elapsed+=dt;if(elapsed-lastControl>.1){lastControl=elapsed;string file=Path.Combine(Out,"control.txt");if(File.Exists(file)){phase=File.ReadAllText(file).Trim();if(phase=="stop"){Shutdown();return;}enabled=phase!="setup";}}
        playerMode?.Frame();
        display?.Update();hud?.Update();dash?.Update();guard?.Update();combat?.Update(wire);healing?.Frame(wire);trapFixture?.Update();interactionProbe?.Update();double wall=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;if(wire.Connected&&wall>=nextMode){nextMode=wall+.5;wire.Send("EP4|MODE|"+HostCombat.Contract+"|"+world.Generation+"|"+modeEpoch+"|"+(world.NativeRequested?"ICEY":"HERO")+"|"+Stopwatch.GetTimestamp());} if(!enabled||!wire.Connected)return; uint focus;GetWindowThreadProcessId(GetForegroundWindow(),out focus);bool ownsFocus=focus==pid;
        int pcMask=BridgeKeys.Read(ownsFocus&&world.NativeRequested&&CarrierGuard.Active);int nextMask=0;float nextVertical=((pcMask&(1<<1))!=0?1:0)-((pcMask&1)!=0?1:0);bool nextButton=(pcMask&(1<<5))!=0;float nextAxis=((pcMask&(1<<3))!=0?1f:0f)-((pcMask&(1<<2))!=0?1f:0f);
        if(nextButton==button&&nextAxis==axis&&nextMask==combatMask&&nextVertical==combatVertical&&pcMask==lastPcMask&&sequence!=0)return;lastPcMask=pcMask;combatMask=nextMask;combatVertical=nextVertical;button=nextButton;axis=nextAxis;sequence++;
        uint foreground;GetWindowThreadProcessId(GetForegroundWindow(),out foreground);long ticks=Stopwatch.GetTimestamp();string utc=DateTime.UtcNow.ToString("o");
        wire.Send("EP4|INPUT|"+sequence+"|"+(button?1:0)+"|"+axis.ToString("R",CultureInfo.InvariantCulture)+"|"+ticks+"|"+Stopwatch.Frequency+"|"+utc+"|"+foreground+"|"+combatMask+"|"+combatVertical.ToString("R",CultureInfo.InvariantCulture)+"|"+pcMask);
        bool j=dc.hxd.Key.Class.isDown.Invoke(0x4A),i=dc.hxd.Key.Class.isDown.Invoke(0x49),k=dc.hxd.Key.Class.isDown.Invoke(0x4B);
        log.WriteLine(string.Join(",",new string[]{utc,ticks.ToString(),Stopwatch.Frequency.ToString(),sequence.ToString(),phase,button.ToString(),axis.ToString("R",CultureInfo.InvariantCulture),foreground.ToString(),(foreground==pid).ToString(),j.ToString(),i.ToString(),k.ToString(),combatMask.ToString(),combatVertical.ToString("R",CultureInfo.InvariantCulture),pcMask.ToString()}));log.Flush();Logger.Information("[EP4D] raw sequence={Seq} phase={Phase} button={Button} axis={Axis} foreground={Focus}",sequence,phase,button,axis,foreground);
    }catch(Exception ex){Logger.Error(ex,"[EP4D] FAIL input prerequisite");Shutdown();}}
    void ChangePlayer(bool native,string reason){if(world.NativeRequested==native)return;combat.SuspendForSwitch();world.SetPlayerMode(native);display.Hide();hud.Hide();dash.Hide();modeEpoch++;nextMode=0;if(wire.Connected)wire.Send("EP4|MODE|"+HostCombat.Contract+"|"+world.Generation+"|"+modeEpoch+"|"+(native?"ICEY":"HERO")+"|"+Stopwatch.GetTimestamp());Logger.Information("[FP50] player={Mode} generation={Gen}; {Reason}",native?"ICEY":"HERO",world.Generation,reason);}
    void Shutdown(){if(stopped)return;stopped=true;Logger.Information("[EP4D] STOP sequence={Seq}",sequence);healing?.Dispose();interactionProbe?.Dispose();playerMode?.Dispose();trapFixture?.Dispose();combat?.Dispose();guard?.Dispose();world?.Dispose();display?.Dispose();hud?.Dispose();dash?.Dispose();wire?.Dispose();log?.Flush();log?.Close();}
    void IOnBeforeGameInit.OnBeforeGameInit(){CpuDisplay.InstallHooks();}
    void IOnGameExit.OnGameExit(){Shutdown();}
}
