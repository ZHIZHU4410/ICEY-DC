using System;
using System.IO;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace EarlyPlaytest {
// Persistent native-runtime arena; terrain remains authoritative in Dead Cells.
public sealed class BridgeArena:MonoBehaviour {
 public static BridgeArena Instance;public static bool Active;public const string Name="IceyDCBridgeArena";
 Scene previous,arena;Rect originalMap,originalPlayer,originalCamera,originalEnemy;Harmony persistence;
 StreamWriter log;float next;bool requested,failed;int savesBlocked;string originalName="";
 void Start(){Instance=this;log=new StreamWriter(Path.Combine(InputAction.Out,"arena.csv"));log.AutoFlush=true;log.WriteLine("utc,stage,activeScene,playerScene,playerInstance,pose,x,y,core,world,sceneGates,mapMinX,mapMaxX,playerMinX,playerMaxX,blockedSaves");}
 void Update(){if(failed)return;try{
  if(Time.realtimeSinceStartup>next){next=Time.realtimeSinceStartup+.2f;requested=File.Exists(Path.Combine(InputAction.Out,"arena.flag"));}
  if(!Active&&requested&&WorldMirror.Instance!=null&&WorldMirror.Instance.Active&&R.Player.gameObject!=null)Create();
  if(Active){Vector3 p=R.Player.transform.position;Rect window=new Rect(p.x-256,p.y-256,512,512);GameArea.MapRange=window;GameArea.PlayerRange=window;GameArea.CameraRange=window;GameArea.EnemyRange=window;R.SceneData.CanAIRun=false;if(Time.frameCount%120==0)Observe("steady");}
 }catch(Exception ex){failed=true;if(InputAction.Instance!=null)InputAction.Instance.Event("ARENA_FAIL "+ex);File.WriteAllText(Path.Combine(InputAction.Out,"arena_failure.txt"),DateTime.UtcNow.ToString("o")+" "+ex);}}
 void Create(){
  previous=SceneManager.GetActiveScene();originalName=previous.name;originalMap=GameArea.MapRange;originalPlayer=GameArea.PlayerRange;originalCamera=GameArea.CameraRange;originalEnemy=GameArea.EnemyRange;Observe("before");
  persistence=new Harmony("local.iceydc.arena.persistence.v4");persistence.Patch(AccessTools.Method(typeof(GameData),"Save"),new HarmonyMethod(typeof(BridgeArena),"GuardSave"));
  persistence.Patch(AccessTools.Method(typeof(SaveData),"Save"),new HarmonyMethod(typeof(BridgeArena),"GuardStorage"));
  CaptureLighting();
  arena=SceneManager.CreateScene(Name);if(!arena.IsValid())throw new InvalidOperationException("empty arena scene invalid");
  WorldMirror.Instance.MoveGeometryTo(arena);
  if(!SceneManager.SetActiveScene(arena))throw new InvalidOperationException("SetActiveScene refused arena");
  Active=true;Vector3 p=R.Player.transform.position;Rect range=new Rect(p.x-256,p.y-256,512,512);GameArea.MapRange=range;GameArea.PlayerRange=range;GameArea.CameraRange=range;GameArea.EnemyRange=range;
  // The player, core, world, UI and native camera already live in DontDestroyOnLoad.
  if(R.Player.gameObject.scene==previous)throw new InvalidOperationException("native player not persistent; original scene retained");
  bool unloaded=SceneManager.UnloadScene(previous);if(!unloaded)throw new InvalidOperationException("native terrain scene unload rejected");
  RestoreLighting();
  R.SceneData.CanAIRun=false;InputAction.Instance.Event("ARENA_ACTIVE previous="+originalName+" scene="+Name+" playerInstance="+R.Player.gameObject.GetInstanceID()+" originalPlayerRange="+originalPlayer+" unloaded=True");Observe("activated");
 }
 Color ambientLight,ambientSky,ambientEquator,ambientGround;float ambientIntensity;UnityEngine.Rendering.AmbientMode ambientMode;UnityEngine.Rendering.SphericalHarmonicsL2 ambientProbe;
 void CaptureLighting(){
  ambientLight=RenderSettings.ambientLight;ambientSky=RenderSettings.ambientSkyColor;ambientEquator=RenderSettings.ambientEquatorColor;ambientGround=RenderSettings.ambientGroundColor;ambientIntensity=RenderSettings.ambientIntensity;ambientMode=RenderSettings.ambientMode;ambientProbe=RenderSettings.ambientProbe;
  InputAction.Instance.Event("ARENA_LIGHT_BEFORE mode="+ambientMode+" ambient="+ambientLight+" sky="+ambientSky+" intensity="+ambientIntensity);
  foreach(Light light in FindObjectsOfType<Light>()){
   InputAction.Instance.Event("ARENA_LIGHT name="+light.name+" type="+light.type+" enabled="+light.enabled+" color="+light.color+" intensity="+light.intensity+" mask="+light.cullingMask+" scene="+light.gameObject.scene.name);
   // Preserve only the native directional illumination, never room geometry or scripts.
   if(light.gameObject.scene!=previous||light.type!=LightType.Directional||!light.enabled)continue;
   GameObject go=new GameObject("BridgeNativeDirectional_"+light.name);DontDestroyOnLoad(go);go.transform.rotation=light.transform.rotation;
   Light copy=go.AddComponent<Light>();copy.type=light.type;copy.color=light.color;copy.intensity=light.intensity;copy.cullingMask=light.cullingMask;copy.renderMode=light.renderMode;copy.shadows=light.shadows;copy.shadowStrength=light.shadowStrength;
   InputAction.Instance.Event("ARENA_LIGHT_PRESERVED "+light.name+" rotation="+go.transform.rotation);
  }
 }
 void RestoreLighting(){
  RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambientLight;RenderSettings.ambientSkyColor=ambientSky;RenderSettings.ambientEquatorColor=ambientEquator;RenderSettings.ambientGroundColor=ambientGround;RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.ambientProbe=ambientProbe;
  InputAction.Instance.Event("ARENA_LIGHT_RESTORED mode="+RenderSettings.ambientMode+" ambient="+RenderSettings.ambientLight+" intensity="+RenderSettings.ambientIntensity);
 }
 static bool GuardSave(ref YieldInstruction __result){if(SceneManager.GetActiveScene().name!=Name)return true;if(Instance!=null)Instance.savesBlocked++;__result=new WaitForEndOfFrame();return false;}
 static bool GuardStorage(ref bool __result){if(SceneManager.GetActiveScene().name!=Name)return true;if(Instance!=null)Instance.savesBlocked++;__result=false;return false;}
 void Observe(string stage){GameObject player=R.Player.gameObject;Vector3 p=player.transform.position;bool core=GameObject.FindGameObjectWithTag("Core")!=null,world=GameObject.FindGameObjectWithTag("World")!=null;log.WriteLine(string.Join(",",new string[]{DateTime.UtcNow.ToString("o"),stage,SceneManager.GetActiveScene().name,player.scene.name,player.GetInstanceID().ToString(),R.Player.Action.stateMachine.currentState,p.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture),p.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture),core.ToString(),world.ToString(),R.SceneGate.GatesInCurrentScene.Count.ToString(),GameArea.MapRange.xMin.ToString("R",System.Globalization.CultureInfo.InvariantCulture),GameArea.MapRange.xMax.ToString("R",System.Globalization.CultureInfo.InvariantCulture),GameArea.PlayerRange.xMin.ToString("R",System.Globalization.CultureInfo.InvariantCulture),GameArea.PlayerRange.xMax.ToString("R",System.Globalization.CultureInfo.InvariantCulture),savesBlocked.ToString()}));}
 void OnDestroy(){if(log!=null)log.Dispose();Instance=null;/* save guard intentionally remains while transient arena is active, until process exits */}
}
}
