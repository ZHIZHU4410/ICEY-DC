using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx;
using HarmonyLib;
using Rewired;
using Rewired.Data;
using UnityEngine;

namespace EarlyPlaytest
{
    [BepInPlugin("local.iceydc.earlyplaytest.input.v4","ICEY native action early playtest","1.0.3")]
    public sealed class InputAction:BaseUnityPlugin
    {
        public static readonly string Out=EpSession.Output;
        public static InputAction Instance;
        public static int Definition=-1,Category=-1,Layout=-1,MapId=-1;
        public static string BootstrapError;
        readonly string control=Path.Combine(Out,"control.txt");
        StreamWriter events,samples,physics;Harmony hook;public GateWire wire;
        CustomController controller;Rewired.Player player;
        bool ready,stopped,oldIgnore,oldBackground,baseline;long baselineConsumed;
        long sequence;bool button;float axis;
        public long ConsumedQpc, SourceQpc, CommitQpc, PhysicsTick, MotorEndQpc; public long Sequence {get{return sequence;}}
        int sampleFrame=-1,downCount,upCount;double nextControl;
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
        static string F(double v){return v.ToString("R",CultureInfo.InvariantCulture);}
        void Awake(){Instance=this;Directory.CreateDirectory(Out);string stamp=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N");events=new StreamWriter(Path.Combine(Out,"input_events_"+stamp+".log"),false);samples=new StreamWriter(Path.Combine(Out,"input_samples_"+stamp+".csv"),false);physics=new StreamWriter(Path.Combine(Out,"physics_"+stamp+".csv"),false);samples.WriteLine("utc,ticks,frequency,unityFrame,sequence,button,down,up,axis,axisRaw,foregroundPid,iceyForeground,downCount,upCount,nativeJumpPressed,nativeJumpDown,nativeJumpUp,nativeMoveLeft,nativeMoveRight,pose,x,y");physics.WriteLine("utc,ticks,frequency,unityFrame,sequence,fixedTime,pose,x,y");oldBackground=Application.runInBackground;Application.runInBackground=true;if(Application.unityVersion!="5.4.0f3"){Event("VERSION MISMATCH expected 5.4.0f3 actual "+Application.unityVersion+"; use tested ICEY build");Stop("version mismatch");return;}hook=new Harmony("local.iceydc.earlyplaytest.input.v4");hook.PatchAll(typeof(InputAction).Assembly);wire=new GateWire("ICEY");wire.Start();gameObject.AddComponent<WorldMirror>();gameObject.AddComponent<BridgeArena>();gameObject.AddComponent<CpuFrames>();gameObject.AddComponent<GuestCombat>();gameObject.AddComponent<NativeHud>();gameObject.AddComponent<DashCapture>();gameObject.AddComponent<GuestRuntimeTrace>();gameObject.AddComponent<BridgeKeyboard>();Event("LOAD pid="+Process.GetCurrentProcess().Id+" runInBackground="+Application.runInBackground);}
        public void Event(string line){if(events!=null){events.WriteLine(DateTime.UtcNow.ToString("o")+" "+line);events.Flush();}Logger.LogInfo("[EP4I] "+line);}
        void Update(){if(stopped)return;try{GuestTestHealing.ProbeFixture();
            if(Time.realtimeSinceStartup>nextControl){nextControl=Time.realtimeSinceStartup+.1;if(File.Exists(control)){string phase=File.ReadAllText(control).Trim();baseline=phase=="standalone";if(phase=="stop"){Stop("control");return;}}}
            if(BootstrapError!=null)throw new InvalidOperationException(BootstrapError);
            if(!ready&&ReInput.isReady&&Definition>=0){
                player=ReInput.players.GetPlayer(0);controller=ReInput.controllers.CreateCustomController(Definition,"EarlyPlaytest");if(controller==null)throw new InvalidOperationException("CreateCustomController returned null for definition "+Definition);
                player.controllers.AddController(controller,false);
                CustomControllerMap map=ReInput.mapping.GetCustomControllerMapInstance(controller,Category,Layout);if(map==null)throw new InvalidOperationException("map factory returned null");
                map.enabled=true;player.controllers.maps.AddMap(controller,map);
                oldIgnore=ReInput.configuration.ignoreInputWhenAppNotInFocus;ReInput.configuration.ignoreInputWhenAppNotInFocus=false;
                ReInput.InputSourceUpdateEvent+=PushInput;ready=true;
                Event("READY Player=0 definition="+Definition+" controller="+controller.id+" buttons="+controller.buttonCount+" axes="+controller.axisCount+" mapCategory="+map.categoryId+" layout="+map.layoutId+" mapElements="+map.AllMaps.Count+" ignoreInputWhenAppNotInFocus=False");
                wire.Send("EP4|READY|ICEY|"+Definition+"|"+controller.id+"|"+Category+"|"+Layout);
            }
        }catch(Exception ex){Event("FAIL "+ex);wire.Send("EP4|FAIL|ICEY|"+ex.Message.Replace('|','/'));Stop("failure");}}
        public int CombatMask;public float CombatVertical;
        void PushInput(){if(stopped||controller==null)return;if(baseline){uint fg;GetWindowThreadProcessId(GetForegroundWindow(),out fg);bool focused=fg==Process.GetCurrentProcess().Id;bool rawButton=focused&&(GetAsyncKeyState(0x20)&0x8000)!=0;float rawAxis=!focused?0:((GetAsyncKeyState(0x44)&0x8000)!=0?1:0)-((GetAsyncKeyState(0x41)&0x8000)!=0?1:0);if(rawButton!=button||rawAxis!=axis||sequence==0){sequence++;button=rawButton;axis=rawAxis;SourceQpc=Stopwatch.GetTimestamp();ConsumedQpc=0;Event("BASELINE_RAW sequence="+sequence+" button="+button+" axis="+axis+" sourceQpc="+SourceQpc);}while(wire.Receive()!=null){}controller.SetButtonValue(0,false);controller.SetAxisValue(0,0);return;}string line;while((line=wire.Receive())!=null){string[] p=line.Split('|');if(p.Length==7&&p[1]=="MODE"&&p[2]==GuestCombat.Contract){bool mode=p[5]=="ICEY";if(mode!=BridgeKeyboard.NativeMode){BridgeKeyboard.NativeMode=mode;BridgeKeyboard.Mask=0;CombatMask=0;axis=CombatVertical=0;button=false;GuestCombat.Instance?.SetPlayerMode(mode);Event("PLAYER_MODE "+p[5]+" generation="+p[3]+" qpc="+p[6]);}continue;}if(GuestTestHealing.Handle(p))continue;if(GuestCombat.Instance!=null&&GuestCombat.Instance.Handle(p))continue;if(p.Length==12&&p[1]=="INPUT"){long next=long.Parse(p[2],CultureInfo.InvariantCulture);if(next<=sequence&&next!=0){Event("DUPLICATE_OR_REORDER sequence="+next);continue;}sequence=next;ConsumedQpc=Stopwatch.GetTimestamp();SourceQpc=long.Parse(p[5]);BridgeKeyboard.Mask=BridgeKeyboard.NativeMode?int.Parse(p[11]):0;CombatMask=BridgeKeyboard.NativeMode?int.Parse(p[9]):0;CombatVertical=float.Parse(p[10],CultureInfo.InvariantCulture);button=p[3]=="1";axis=float.Parse(p[4],CultureInfo.InvariantCulture);Event("CONSUME sequence="+sequence+" button="+button+" axis="+F(axis)+" dcTicks="+p[5]+" dcUtc="+p[7]);wire.Send("EP4|CONSUMED|"+sequence+"|"+Stopwatch.GetTimestamp()+"|"+Stopwatch.Frequency+"|"+DateTime.UtcNow.ToString("o"));}}
            for(int i=0;i<8;i++)controller.SetButtonValue(i,(CombatMask&(1<<i))!=0);controller.SetAxisValue(0,axis);controller.SetAxisValue(1,CombatVertical);
        }
        public void Sample(){if(stopped||!ready||sampleFrame==Time.frameCount)return;sampleFrame=Time.frameCount;try{
            bool held=player.GetButton("Cross"),down=player.GetButtonDown("Cross"),up=player.GetButtonUp("Cross");float value=player.GetAxis("LSHorizontal"),raw=player.GetAxisRaw("LSHorizontal");if(down)downCount++;if(up)upCount++;
            uint foreground;GetWindowThreadProcessId(GetForegroundWindow(),out foreground);string utc=DateTime.UtcNow.ToString("o");string pose="";float x=0,y=0;
            try{if(R.Player.gameObject!=null){pose=R.Player.Action.stateMachine.currentState;Vector3 position=R.Player.transform.position;x=position.x;y=position.y;}}catch{}
            bool jump=Core.Input.Game.Jump.Pressed,jumpDown=Core.Input.Game.Jump.OnPressed,jumpUp=Core.Input.Game.Jump.OnReleased,left=Core.Input.Game.MoveLeft.Pressed,right=Core.Input.Game.MoveRight.Pressed;
            if(baseline&&baselineConsumed!=sequence&&jump==button&&(axis==0?!left&&!right:axis>0?right:left)){ConsumedQpc=Stopwatch.GetTimestamp();baselineConsumed=sequence;Event("BASELINE_CONSUMED sequence="+sequence+" qpc="+ConsumedQpc);}
            samples.WriteLine(string.Join(",",new string[]{utc,Stopwatch.GetTimestamp().ToString(),Stopwatch.Frequency.ToString(),Time.frameCount.ToString(),sequence.ToString(),held.ToString(),down.ToString(),up.ToString(),F(value),F(raw),foreground.ToString(),(foreground == Process.GetCurrentProcess().Id).ToString(),downCount.ToString(),upCount.ToString(),jump.ToString(),jumpDown.ToString(),jumpUp.ToString(),left.ToString(),right.ToString(),pose.Replace(',',';'),F(x),F(y)}));
            if(down||up||jumpDown||jumpUp){Event("EDGE sequence="+sequence+" rewiredDown="+down+" rewiredUp="+up+" nativeJumpDown="+jumpDown+" nativeJumpUp="+jumpUp+" pose="+pose+" totalDown="+downCount+" totalUp="+upCount);samples.Flush();}
            if(Time.frameCount%30==0)samples.Flush();wire.Send("EP4|SAMPLE|"+sequence+"|"+(held?1:0)+"|"+(down?1:0)+"|"+(up?1:0)+"|"+F(value)+"|"+F(raw)+"|"+Stopwatch.GetTimestamp()+"|"+Stopwatch.Frequency+"|"+utc+"|"+foreground);
        }catch(Exception ex){Event("FAIL sample "+ex);Stop("failure");}}
        public void ObservePhysics(PlatformMovement movement){if(stopped||!ready||sequence==0||physics==null)return;try{if(R.Player.gameObject==null||movement.gameObject!=R.Player.gameObject)return;MotorEndQpc=Stopwatch.GetTimestamp();Vector3 p=R.Player.transform.position;string pose=R.Player.Action.stateMachine.currentState;physics.WriteLine(string.Join(",",new string[]{DateTime.UtcNow.ToString("o"),Stopwatch.GetTimestamp().ToString(),Stopwatch.Frequency.ToString(),Time.frameCount.ToString(),sequence.ToString(),F(Time.fixedTime),pose.Replace(',',';'),F(p.x),F(p.y)}));if(Time.frameCount%30==0)physics.Flush();}catch(Exception ex){Event("FAIL physics "+ex);Stop("failure");}}
        void Stop(string reason){if(stopped)return;stopped=true;Event("STOP "+reason+" down="+downCount+" up="+upCount);if(ready){BridgeKeyboard.Mask=0;ReInput.InputSourceUpdateEvent-=PushInput;for(int ci=0;ci<8;ci++)controller.SetButtonValue(ci,false);controller.SetAxisValue(0,0);controller.SetAxisValue(1,0);player.controllers.RemoveController(controller);ReInput.controllers.DestroyCustomController(controller);ReInput.configuration.ignoreInputWhenAppNotInFocus=oldIgnore;}if(hook!=null)hook.UnpatchSelf();if(wire!=null)wire.Dispose();Application.runInBackground=oldBackground;if(samples!=null){samples.Flush();samples.Close();samples=null;}if(physics!=null){physics.Flush();physics.Close();physics=null;}if(events!=null){events.Flush();events.Close();events=null;}}
        void OnDestroy(){Stop("destroy");Instance=null;}
    }
    [HarmonyPatch(typeof(InputManager_Base),"Awake")]
    static class DefinitionBootstrap
    {
        [HarmonyPrefix]static void Before(InputManager_Base __instance){try{
            UserData data=__instance.userData;if(data==null)throw new InvalidOperationException("InputManager userData null before Awake");
            data.AddCustomController();int[] ids=data.GetCustomControllerIds();int id=ids[ids.Length-1];CustomController_Editor def=data.GetCustomControllerById(id);def.AddAxis();def.AddAxis();for(int ci=0;ci<8;ci++)def.AddButton();
            int[] layouts=data.GetCustomControllerLayoutIds();if(layouts.Length==0){data.AddCustomControllerLayout();layouts=data.GetCustomControllerLayoutIds();}int layout=layouts[0],category=data.GetMapCategoryIds()[0];
            bool createReturn=data.CreateCustomControllerMap(category,id,layout);
            Rewired.Data.Mapping.ControllerMap_Editor map=data.GetCustomControllerMap(category,id,layout);
            if(map==null)throw new InvalidOperationException("CreateCustomControllerMap did not yield a map object");
            if(InputAction.Instance!=null)InputAction.Instance.Event("MAP_CREATE return="+createReturn+" mapObjectExists=True id="+map.id);
            int actionButton=data.GetActionId("Cross"),actionAxis=data.GetActionId("LSHorizontal");if(actionButton<0||actionAxis<0)throw new InvalidOperationException("probe actions missing");
            map.actionElementMaps.Clear();string[] nativeActions={"Cross","Square","Triangle","R1","L1","Circle","L2","R2"};for(int ci=0;ci<nativeActions.Length;ci++){int action=data.GetActionId(nativeActions[ci]);if(action<0)throw new InvalidOperationException("Missing native combat action "+nativeActions[ci]);map.actionElementMaps.Add(new ActionElementMap(action,ControllerElementType.Button,def.GetButtonElementIdentifierIds()[ci]));}map.actionElementMaps.Add(new ActionElementMap(actionAxis,ControllerElementType.Axis,def.GetAxisElementIdentifierIds()[0],Pole.Positive,AxisRange.Full));int verticalAction=data.GetActionId("LSVertical");if(verticalAction<0)throw new InvalidOperationException("Missing LSVertical");map.actionElementMaps.Add(new ActionElementMap(verticalAction,ControllerElementType.Axis,def.GetAxisElementIdentifierIds()[1],Pole.Positive,AxisRange.Full));
            InputAction.Definition=id;InputAction.Category=category;InputAction.Layout=layout;InputAction.MapId=data.GetCustomControllerMapId(category,id,layout);
            if(InputAction.Instance!=null)InputAction.Instance.Event("DEFINITION before InputManager.Awake id="+id+" category="+category+" layout="+layout+" actionCross="+actionButton+" actionRS="+actionAxis+" buttonElement="+def.GetButtonElementIdentifierIds()[0]+" axisElement="+def.GetAxisElementIdentifierIds()[0]+" mapCount="+map.actionElementMaps.Count);
        }catch(Exception ex){InputAction.BootstrapError=ex.ToString();}}
    }
    [HarmonyPatch(typeof(GameWorld.Joystick),"Update")]static class NativeInputConsumer{[HarmonyPostfix]static void After(){if(InputAction.Instance!=null)InputAction.Instance.Sample();}}
    [HarmonyPatch(typeof(PlatformMovement),"FixedUpdate")]static class NativePhysicsConsumer{[HarmonyPrefix]static void Before(PlatformMovement __instance){if(WorldMirror.Instance!=null)WorldMirror.Instance.BeforeMove(__instance);}[HarmonyPostfix]static void After(PlatformMovement __instance){if(InputAction.Instance!=null)InputAction.Instance.ObservePhysics(__instance);if(WorldMirror.Instance!=null)WorldMirror.Instance.AfterMove(__instance);}}
}
