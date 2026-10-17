using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
namespace EarlyPlaytest {
// Mirrors the host's actual local solid cells. No reimplementation of the guest motor.
public sealed class WorldMirror:MonoBehaviour {
 public static WorldMirror Instance;public bool Active;public int Generation;public long Revision;
 public double HostX,HostY;FrameSlot slot;byte[] data=new byte[FrameSlot.Size];
 readonly List<GameObject> shapes=new List<GameObject>();readonly List<Collider2D> isolated=new List<Collider2D>();
 readonly Dictionary<string,GameObject> terrain=new Dictionary<string,GameObject>();int oldOneWayMask;PlatformMovement configuredMotor;
 Vector2 anchor;float s;bool built,stopped;int warm;long candidate;StreamWriter log;float nextControl;bool requested;
 void Start(){Instance=this;slot=new FrameSlot(@"Local\IceyDCBridge_EP4_WORLD_FP50");log=new StreamWriter(Path.Combine(InputAction.Out,"world_mirror.csv"));log.WriteLine("utc,stage,revision,generation,shapes,isolated,prepareMs,unityFrame,fixedTime,grounded,footX,footY,hostX,hostY,rayLayer");log.AutoFlush=true;}
 void Update(){if(stopped)return;try{if(Time.realtimeSinceStartup>nextControl){nextControl=Time.realtimeSinceStartup+.1f;requested=File.Exists(Path.Combine(InputAction.Out,"world.flag"));if(File.ReadAllText(Path.Combine(InputAction.Out,"control.txt")).Trim()=="stop"){Dispose();return;}}if(!requested||Application.loadedLevelName=="ui_start"||R.Player.gameObject==null)return;if(slot.Read(data,candidate)){candidate=BitConverter.ToInt64(data,8);Build();}}catch(Exception e){InputAction.Instance.Event("WORLD_FAIL "+e);Dispose();}}
 void Build(){long start=Stopwatch.GetTimestamp();var box=R.Player.gameObject.GetComponent<BoxCollider2D>();int generation=(int)BitConverter.ToInt64(data,16);if(!built||generation!=Generation){foreach(GameObject go in shapes){go.GetComponent<BoxCollider2D>().enabled=false;Destroy(go);}shapes.Clear();terrain.Clear();anchor=new Vector2(box.bounds.center.x,box.bounds.min.y);s=box.bounds.size.y/(float)(BitConverter.ToDouble(data,40)/24.0);HostX=BitConverter.ToDouble(data,24);HostY=BitConverter.ToDouble(data,32);Generation=generation;Active=false;warm=0;}
 int x0=BitConverter.ToInt32(data,64),y0=BitConverter.ToInt32(data,68),w=BitConverter.ToInt32(data,72),h=BitConverter.ToInt32(data,76);if(w<=0||h<=0||w*h>16000)throw new InvalidOperationException("invalid geometry extent");int layer=0;while(layer<31&&(LayerManager.GroundMask&(1<<layer))==0)layer++;int oneWay=LayerManager.OneWayPlatformLayerID;if(layer>=31||oneWay<0||LayerManager.OneWayGroundMask==0||oneWay==layer)throw new InvalidOperationException("native solid/OneWayPlatforms layers unavailable");int n=0,platforms=0;
 // Preserve collider identity by world cell, so native timed IgnoreCollider never follows a reused pool object.
 foreach(GameObject go in shapes)go.GetComponent<BoxCollider2D>().enabled=false;
 for(int y=0;y<h;y++){int begin=-1;for(int x=0;x<=w;x++){int cell=x<w?BitConverter.ToInt32(data,FrameSlot.Header+(y*w+x)*4):0;bool solid=(cell&1)!=0&&(cell&8)==0;
  if((cell&9)==9){Shape(x0+x,y0+y,1,true,oneWay);n++;platforms++;}
  if(solid&&begin<0)begin=x;if(!solid&&begin>=0){Shape(x0+begin,y0+y,x-begin,false,layer);n++;begin=-1;}
 }}
 InputAction.Instance.Event("WORLD_TYPES solidRuns="+(n-platforms)+" oneWayCells="+platforms+" nativeOneWayLayer="+oneWay+" DC oneWayBit=8; native JumpDown unchanged");
 Revision=candidate;built=true;InputAction.Instance.Event("WORLD_BUILD revision="+Revision+" generation="+Generation+" shapes="+n+" scale="+s+" ms="+((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency));Write("build",(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency);
 }
 void Shape(int x,int y,int widthCells,bool oneWay,int layer){string key=Generation+":"+x+":"+y+":"+widthCells+":"+oneWay;GameObject go;if(!terrain.TryGetValue(key,out go)){go=new GameObject("EP4_DC_cellrun_"+key);go.layer=layer;go.AddComponent<BoxCollider2D>();terrain.Add(key,go);shapes.Add(go);}float width=widthCells*s,height=oneWay?s*.1f:s;go.transform.position=new Vector3(anchor.x+(float)(x-HostX)*s+width*.5f,anchor.y-(float)(y-HostY)*s-height*.5f,0);var c=go.GetComponent<BoxCollider2D>();c.size=new Vector2(width,height);c.enabled=true;}
 public void BeforeMove(PlatformMovement m){if(stopped||!requested||!built||m.gameObject!=R.Player.gameObject)return;if(configuredMotor!=m){configuredMotor=m;oldOneWayMask=m.m_oneWayLayer.value;m.m_oneWayLayer=oldOneWayMask|LayerManager.OneWayGroundMask;}if(!Active){warm++;if(warm<3)return;foreach(Collider2D c in FindObjectsOfType<Collider2D>()){if(!c.enabled||c.transform.IsChildOf(R.Player.transform)||c.gameObject.name.StartsWith("EP4_DC_cellrun_"))continue;c.enabled=false;isolated.Add(c);}Active=true;InputAction.Instance.Event("WORLD_ACTIVE generation="+Generation+" isolated="+isolated.Count+" scale="+s+" oneWayMask="+m.m_oneWayLayer.value);Write("activate",0);} }
 public void AfterMove(PlatformMovement m){if(Active&&m.gameObject==R.Player.gameObject)Write("motor_end",0);}
 void Write(string stage,double ms){var b=R.Player.gameObject.GetComponent<BoxCollider2D>().bounds;var motor=R.Player.gameObject.GetComponent<PlatformMovement>();double hx=HostX+(b.center.x-anchor.x)/s,hy=HostY-(b.min.y-anchor.y)/s;log.WriteLine(string.Join(",",new string[]{DateTime.UtcNow.ToString("o"),stage,Revision.ToString(),Generation.ToString(),shapes.Count.ToString(),isolated.Count.ToString(),ms.ToString("R",System.Globalization.CultureInfo.InvariantCulture),Time.frameCount.ToString(),Time.fixedTime.ToString("R",System.Globalization.CultureInfo.InvariantCulture),motor.State.IsDetectedGround.ToString(),b.center.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture),b.min.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture),hx.ToString("R",System.Globalization.CultureInfo.InvariantCulture),hy.ToString("R",System.Globalization.CultureInfo.InvariantCulture),motor.m_rayLayer.value.ToString()}));}
 public void Stamp(byte[] packet){if(Active){FrameSlot.D(packet,144,anchor.x);FrameSlot.D(packet,152,anchor.y);FrameSlot.D(packet,136,s);}FrameSlot.D(packet,160,HostX);FrameSlot.D(packet,168,HostY);FrameSlot.I(packet,176,Active?Generation:0);FrameSlot.L(packet,248,Revision);}
 public Vector2 FromHost(double x,double y){return new Vector2(anchor.x+(float)(x-HostX)*s,anchor.y-(float)(y-HostY)*s);} public float Scale=>s;
 public void MoveGeometryTo(UnityEngine.SceneManagement.Scene scene){foreach(GameObject go in shapes)if(go!=null)UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);}
 void Dispose(){if(stopped)return;stopped=true;Active=false;if(configuredMotor!=null)configuredMotor.m_oneWayLayer=oldOneWayMask;foreach(Collider2D c in isolated)if(c!=null)c.enabled=true;foreach(GameObject go in shapes)if(go!=null)Destroy(go);slot?.Dispose();log?.Dispose();}
 void OnDestroy(){Dispose();Instance=null;}
}}
