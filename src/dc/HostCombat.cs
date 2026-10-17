using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Math=System.Math;
using dc;
using dc.en;
using dc.tool.atk;
namespace EarlyPlaytest;
public sealed class HostCombat:IDisposable {
 public static HostCombat Instance;public const string Contract="FP50-B";
 readonly string output;readonly StreamWriter log;readonly FrameSlot slot=new FrameSlot(@"Local\IceyDCBridge_FP50_ENEMIES_B");
 readonly byte[] data=new byte[FrameSlot.Size];readonly Dictionary<int,Entity> entities=new();readonly HashSet<string> applied=new();
 readonly Queue<IncomingEvent> incoming=new();readonly Dictionary<long,AttackData> pending=new();
 [ThreadStatic]static long currentGuestEvent;
 [ThreadStatic]static int acknowledgedTrap;[ThreadStatic]static double acknowledgedLoss;[ThreadStatic]static bool trapAcknowledged;[ThreadStatic]static TrapContinuation trapContext;
 readonly Dictionary<int,TrapContinuation> traps=new();
 sealed class TrapContinuation{public dc.en.ltrap.Spikes trap;public Entity target;public dc.en.ltrap.Hook_Spikes.orig_onTouch continuation;public long eventId;}
 [ThreadStatic]static int acknowledgedBullet;[ThreadStatic]static dc.HitResult acknowledgedResult;
 readonly Dictionary<int,BulletContinuation> bullets=new();readonly Dictionary<long,long> pendingSince=new();int queuePeak;
 sealed class BulletContinuation{public Bullet bullet;public Entity target;public Hook_Bullet.orig_onTouchValidTarget continuation;public long eventId;}
 static readonly HaxeProxy.Runtime.HlAction<Entity,AttackData> noBeforeHit=(entity,attack)=>{};
 public static bool ApplyAcknowledgedBullet(AttackData attack,Entity target){if(target==CarrierGuard.Hero&&acknowledgedTrap!=0&&(attack.source??attack.carrier)?.__uid==acknowledgedTrap){attack.hitResult=acknowledgedResult;attack.rawFinalDmg=acknowledgedLoss;trapAcknowledged=true;return true;}if(acknowledgedBullet==0||target!=CarrierGuard.Hero||attack.carrier?.__uid!=acknowledgedBullet)return false;attack.hitResult=acknowledgedResult;return true;}

 readonly List<DropWatch> drops=new();sealed class DropWatch{public int uid;public long eventId;public double until,x,y;public dc.pr.Level level;public HashSet<int> known;}
 GateWire wire;long revision,eventId;double next;int generation;bool disposed,enabled;
 sealed class IncomingEvent{public long id,qpc;public int uid,senderUid;public double damage;public string kind;}
 public HostCombat(string output){this.output=output;Instance=this;log=new StreamWriter(Path.Combine(output,"combat_host.csv"));log.AutoFlush=true;log.WriteLine("utc,qpc,frameId,postsolveTickId,generation,eventId,sourceUid,targetUid,event,amount,result");Hook_Mob.onDie+=OnDie;Hook_Mob.dropLoot+=DropLoot;Hook_Bullet.onTouchValidTarget+=BulletTouch;Hook_Bullet.fixedUpdate+=BulletFixed;dc.en.ltrap.Hook_Spikes.onTouch+=TrapTouch;}
 public static bool Enabled=>Instance!=null&&Instance.enabled&&CarrierGuard.Active;
 public void Write(string type,long id,int source,int target,double amount,string result){log.WriteLine(string.Join(",",DateTime.UtcNow.ToString("o"),Stopwatch.GetTimestamp(),CpuDisplay.Instance?.CurrentFrameId??0,CpuDisplay.Instance?.CurrentTickId??0,HostWorld.Instance?.Generation??0,id,source,target,type,amount.ToString("R",CultureInfo.InvariantCulture),result.Replace(',',';')));}
 public void Update(GateWire connection){if(disposed)return;wire=connection;enabled=CarrierGuard.Active&&File.Exists(Path.Combine(output,"combat.flag"));string raw;while((raw=wire.Receive())!=null){var fields=raw.Split('|');if(BridgeKeys.Handle(fields)||TestHealing.Instance?.Handle(fields)==true)continue;if(fields.Length>2&&fields[0]=="EP4"&&(fields[1]=="HIT"||fields[1]=="DEFENSE"))Handle(fields);else Write("unexpected_guest_message",0,0,0,0,raw);}if(!CarrierGuard.Active)return;
  var h=CarrierGuard.Hero;enabled=File.Exists(Path.Combine(output,"combat.flag"));int g=HostWorld.Instance.Generation;
  if(generation!=g){generation=g;entities.Clear();applied.Clear();incoming.Clear();pending.Clear();pendingSince.Clear();bullets.Clear();traps.Clear();Write("generation",0,0,h.__uid,0,"Old combat identities discarded");}

  double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;CheckTimeouts();if(now>=next){next=now+.05;ObserveDrops(now);Publish();Write("world_identity",0,h.__uid,h.__uid,h.life,"levelGameHero="+(h._level.game.hero==h)+" teamHero="+(h._team==h._level.teamHero)+" touch="+h.hasEntityTouchChecks);}
  if(enabled&&incoming.Count>0&&Publish())while(incoming.Count>0){var e=incoming.Dequeue();wire.Send(string.Join("|","EP4","INCOMING",Contract,generation,e.id,e.uid,e.damage.ToString("R",CultureInfo.InvariantCulture),h.maxLife,e.qpc,e.kind,e.senderUid));Write("incoming_forward",e.id,e.uid,h.__uid,e.damage,e.kind);}
 }
 bool Publish(){var h=CarrierGuard.Hero;if(h==null||h._level==null)return false;
  var live=new Dictionary<int,Entity>();var list=h._level.entities;
  for(int i=0;i<list.length;i++){var entity=list.getDyn(i) as Entity;if(entity is Mob&&!entity.destroyed&&entity.life>0&&Math.Abs(entity.cx+entity.xr-h.cx-h.xr)<35&&Math.Abs(entity.cy+entity.yr-h.cy-h.yr)<28)live[entity.__uid]=entity;}
  foreach(var attack in pending.Values){var source=attack.source??attack.carrier;var sender=attack.carrier??source;if(source!=null&&!source.destroyed&&source._level==h._level)live[source.__uid]=source;if(sender!=null&&!sender.destroyed&&sender._level==h._level)live[sender.__uid]=sender;}
  foreach(var e in incoming)if(entities.TryGetValue(e.uid,out var source)&&!source.destroyed)live[e.uid]=source;
  if(live.Count>256)throw new InvalidOperationException("Combat snapshot capacity exceeded; not safe to drop targets");
  entities.Clear();foreach(var pair in live)entities.Add(pair.Key,pair.Value);
  FrameSlot.I(data,0,0x34504645);FrameSlot.I(data,4,4);FrameSlot.L(data,8,++revision);FrameSlot.L(data,16,generation);FrameSlot.I(data,24,live.Count);
  int index=0;foreach(var pair in live){var e=pair.Value;int o=FrameSlot.Header+index++*72;FrameSlot.I(data,o,e.__uid);FrameSlot.I(data,o+4,e is Mob m?(1|(m.isUnconscious()?2:0)):0);FrameSlot.D(data,o+8,e.cx+e.xr);FrameSlot.D(data,o+16,e.cy+e.yr);FrameSlot.D(data,o+24,e.radius);FrameSlot.D(data,o+32,e.hei);FrameSlot.I(data,o+40,e.life);FrameSlot.I(data,o+44,e.maxLife);int direction=e.dir;if(e is Bullet bullet){double speed=bullet.dx+bullet.bdx;if(Math.Abs(speed)>1e-9)direction=Math.Sign(speed);}FrameSlot.I(data,o+48,direction);FrameSlot.D(data,o+56,e.dx+e.bdx);FrameSlot.D(data,o+64,e.dy+e.bdy);FrameSlot.I(data,o+52,e is dc.en.mob.Boss?2:e is Mob mob&&mob.elite?1:0);}
  return slot.Write(data);
 }
 public void Incoming(AttackData a,Entity target,string path){
  if(!Enabled||!File.Exists(Path.Combine(output,"defense.flag"))){Write("incoming_quarantined",0,a.source?.__uid??0,target.__uid,0,"Native defense channel gated for isolated outgoing test");return;}
  var source=a.source??a.carrier;if(source==null){Write("FAIL_unsourced_attack",0,0,target.__uid,0,path);return;}
  double amount;
  try{amount=dc.Const.Class.scaleMobValueToTier.Invoke(a.baseDmg,a.sourceTier)*a.dmgMultiplier;}catch(Exception ex){Write("FAIL_damage_decode",0,source.__uid,target.__uid,0,ex.GetType().Name+" "+a.baseDmg?.GetType().FullName);return;}
  if(double.IsNaN(amount)||double.IsInfinity(amount)||amount<0){Write("FAIL_damage_range",0,source.__uid,target.__uid,amount,path);return;}
  if(incoming.Count>=64||pending.Count>=128)throw new InvalidOperationException("Combat event capacity exceeded; explicit failure");
  long id=++eventId;if(trapContext!=null&&trapContext.trap.__uid==source.__uid){trapContext.eventId=id;traps[source.__uid]=trapContext;}entities[source.__uid]=source;pending.Add(id,a);pendingSince[id]=Stopwatch.GetTimestamp();var sender=a.carrier??source;entities[sender.__uid]=sender;incoming.Enqueue(new IncomingEvent{id=id,qpc=Stopwatch.GetTimestamp(),uid=source.__uid,senderUid=sender.__uid,damage=amount,kind=source.GetType().Name+"/"+sender.GetType().Name});queuePeak=Math.Max(queuePeak,incoming.Count);Write("dc_attack_hit",id,source.__uid,target.__uid,amount,path+"; sourceTier="+a.sourceTier+" senderUid="+sender.__uid+" senderKind="+sender.GetType().Name+" incomingQueue="+incoming.Count+" pending="+pending.Count+" queuePeak="+queuePeak);
 }
 void Handle(string[] p){
  if(!enabled){Write("rejected_gate_closed",0,0,0,0,p[1]);return;}
  if(p.Length<5||p[2]!=Contract||!int.TryParse(p[3],out int g)||g!=generation){Write("rejected_contract_or_generation",0,0,0,0,string.Join("|",p));return;}
  if(p[1]=="HIT"){
   if(p.Length!=12){Write("rejected_shape",0,0,0,0,"HIT");return;}long id=long.Parse(p[4]);int uid=int.Parse(p[5]);double damage=double.Parse(p[6],CultureInfo.InvariantCulture);string key=g+"/"+uid+"/"+id;
   if(applied.Contains(key)){Write("rejected_duplicate",id,CarrierGuard.Hero.__uid,uid,damage,key);return;}
   if(!entities.TryGetValue(uid,out var target)||target.destroyed||target.life<=0||target._level!=CarrierGuard.Hero._level||target is not Mob){Write("rejected_stale_target",id,0,uid,damage,key);return;}
   if(damage<=0||damage>1000000){Write("rejected_damage",id,0,uid,damage,key);return;}
   applied.Add(key);int before=target.life;var attack=AttackUtils.Class.createFromEntity.Invoke(CarrierGuard.Hero,(object)damage);attack.useHeroScaling=false;
   currentGuestEvent=id;CarrierGuard.GuestHitDepth++;try{AttackUtils.Class.hit.Invoke(attack,target);}finally{CarrierGuard.GuestHitDepth--;currentGuestEvent=0;}
   Write("guest_hit_applied",id,CarrierGuard.Hero.__uid,uid,damage,"nativeAtk="+p[7]+" guestReadyFrame="+p[8]+" guestTick="+p[9]+" lifeBefore="+before+" lifeAfter="+target.life+" hitResult="+attack.hitResult.GetType().Name+" eventQpc="+p[10]+" origin="+p[11]);
  }else if(p[1]=="DEFENSE"){
   if(p.Length!=12){Write("rejected_shape",0,0,0,0,"DEFENSE");return;}long id=long.Parse(p[4]);int uid=int.Parse(p[5]);if(!pending.TryGetValue(id,out var attack)){Write("rejected_unknown_defense",id,uid,0,0,"No pending attack");return;}if((attack.source??attack.carrier)?.__uid!=uid){Write("rejected_defense_source",id,uid,0,0,"Source UID does not match pending native attack");return;}
   int hp=int.Parse(p[7]),max=int.Parse(p[8]);if(max<=0||hp<0||hp>max){Write("rejected_hp",id,uid,0,hp,p[6]);return;}
   pending.Remove(id);pendingSince.Remove(id);var h=CarrierGuard.Hero;if(CpuDisplay.Instance==null||CpuDisplay.Instance.CurrentReadyQpc<long.Parse(p[11]))h.life=(int)Math.Ceiling(h.maxLife*(hp/(double)max));
   ApplyNativeWorldResult(id,attack,h,p[6],double.Parse(p[9],CultureInfo.InvariantCulture)*h.maxLife/max);
   Write("native_defense_world_result",id,uid,h.__uid,double.Parse(p[9],CultureInfo.InvariantCulture),p[6]+" nativeHp="+hp+"/"+max+" energy="+p[10]+" guestQpc="+p[11]+"; DC HP field reflects guest; no DC damage calculation");
  }
 }
 void CheckTimeouts(){long now=Stopwatch.GetTimestamp();foreach(var pair in pendingSince)if(now-pair.Value>Stopwatch.Frequency*3){Write("FAIL_native_ack_timeout",pair.Key,0,CarrierGuard.Hero.__uid,0,"3s pending; incoming defense closed explicitly; no synthetic success");string gate=Path.Combine(output,"defense.flag");if(File.Exists(gate))File.Move(gate,Path.Combine(output,"defense_timeout_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+".flag"));pending.Clear();pendingSince.Clear();incoming.Clear();bullets.Clear();traps.Clear();break;}}
 static void TrapTouch(dc.en.ltrap.Hook_Spikes.orig_onTouch orig,dc.en.ltrap.Spikes self,Entity target){var i=Instance;if(!Enabled||target!=CarrierGuard.Hero||!File.Exists(Path.Combine(i.output,"defense.flag"))){orig(self,target);return;}if(i.traps.ContainsKey(self.__uid))return;var previous=trapContext;trapContext=new TrapContinuation{trap=self,target=target,continuation=orig};try{orig(self,target);}finally{trapContext=previous;}}
 void ApplyTrapContinuation(long id,AttackData attack,Hero target,double loss){int uid=(attack.source??attack.carrier)?.__uid??0;if(!traps.TryGetValue(uid,out var continuation))return;traps.Remove(uid);if(continuation.eventId!=id||continuation.trap.destroyed||continuation.trap._level!=target._level){Write("FAIL_trap_continuation_identity",id,uid,target.__uid,0,"Wrong event/destroyed/level");return;}acknowledgedTrap=uid;acknowledgedLoss=loss;acknowledgedResult=attack.hitResult;trapAcknowledged=false;try{continuation.continuation(continuation.trap,target);Write(trapAcknowledged?"trap_continuation_applied":"FAIL_trap_continuation_precondition",id,uid,target.__uid,loss,"Native result="+attack.hitResult.GetType().Name+" nativeTrapCooldown="+target.cd.fastCheck.exists(1826619392)+" blood="+continuation.trap.blood+"; no Hero HP pipeline");}finally{acknowledgedTrap=0;acknowledgedResult=null;}}
 static void BulletFixed(Hook_Bullet.orig_fixedUpdate orig,Bullet self){if(Instance!=null&&Instance.bullets.ContainsKey(self.__uid))return;orig(self);}
 static void BulletTouch(Hook_Bullet.orig_onTouchValidTarget orig,Bullet self,Entity target){var i=Instance;if(!Enabled||target!=CarrierGuard.Hero||!File.Exists(Path.Combine(i.output,"defense.flag"))||self.atk==null||self.atk.hasNoBaseDamage()){orig(self,target);return;}if(i.bullets.ContainsKey(self.__uid))return;
  self.beforeHit.Invoke(target,self.atk);long previous=i.eventId;i.Incoming(self.atk,target,"Deferred native Bullet.onTouchValidTarget");if(i.eventId==previous){i.Write("FAIL_bullet_enqueue",0,self.__uid,target.__uid,0,"No native event enqueued");return;}i.bullets[self.__uid]=new BulletContinuation{bullet=self,target=target,continuation=orig,eventId=i.eventId};i.Write("bullet_deferred",i.eventId,self.atk.source?.__uid??0,self.__uid,0,"Native bullet continuation retained until ICEY defense decision; beforeHit executed once");}
 void ApplyNativeWorldResult(long id,AttackData attack,Hero target,string outcome,double loss){attack.lastHitTarget=target;attack.rawFinalDmg=loss;attack.hitResult=outcome=="native_invulnerable"||outcome=="native_filtered"?new dc.HitResult.Miss():outcome=="native_flash_counter_window"?new dc.HitResult.Block():new dc.HitResult.Normal();
  if(attack.hitResult is not dc.HitResult.Miss){attack.source?.attackSource?.onHit?.Invoke(attack,target);attack.onHit?.Invoke(attack.carrier??attack.source,target);Write("native_world_source_callbacks",id,attack.source?.__uid??0,attack.carrier?.__uid??0,loss,"hitResult="+attack.hitResult.GetType().Name+" AttackData.onHit="+(attack.onHit!=null)+"; Hero attackTarget/HP pipeline bypassed");}
  int sender=attack.carrier?.__uid??0;if(sender!=0&&bullets.TryGetValue(sender,out var pendingBullet)){bullets.Remove(sender);if(pendingBullet.eventId!=id||pendingBullet.bullet.destroyed||pendingBullet.bullet._level!=target._level){Write("FAIL_bullet_continuation_identity",id,attack.source?.__uid??0,sender,0,"Destroyed or wrong event/level");return;}var before=pendingBullet.bullet.beforeHit;acknowledgedBullet=sender;acknowledgedResult=attack.hitResult;try{pendingBullet.bullet.beforeHit=noBeforeHit;pendingBullet.continuation(pendingBullet.bullet,target);Write("bullet_continuation_applied",id,attack.source?.__uid??0,sender,0,"nativeResult="+acknowledgedResult.GetType().Name+" destroyed="+pendingBullet.bullet.destroyed+"; native onBulletHit/block/pierce/destruction resumed; no DC player damage calculation");}finally{pendingBullet.bullet.beforeHit=before;acknowledgedBullet=0;acknowledgedResult=null;}}
  ApplyTrapContinuation(id,attack,target,loss);
 }
 static void OnDie(Hook_Mob.orig_onDie orig,Mob self){var i=Instance;i?.Write("dc_mob_death",currentGuestEvent,currentGuestEvent>0?CarrierGuard.Hero?.__uid??0:0,self.__uid,self.life,"Native Mob.onDie");if(Enabled&&i.wire!=null)i.wire.Send(string.Join("|","EP4","MOBDEAD",Contract,i.generation,self.__uid,(self.cx+self.xr).ToString("R",CultureInfo.InvariantCulture),(self.cy+self.yr).ToString("R",CultureInfo.InvariantCulture),Stopwatch.GetTimestamp()));orig(self);}
 void ObserveDrops(double now){for(int i=drops.Count-1;i>=0;i--){var w=drops[i];for(int n=0;n<w.level.entities.length;n++){var e=w.level.entities.getDyn(n) as Entity;if(e==null||!w.known.Add(e.__uid)||e is not Loot||Math.Abs(e.cx+e.xr-w.x)>8||Math.Abs(e.cy+e.yr-w.y)>5)continue;Write("dc_drop_new_entity",w.eventId,w.uid,e.__uid,0,e.GetType().FullName+" x="+(e.cx+e.xr)+" y="+(e.cy+e.yr)+"; native deferred loot observed");}if(now>w.until){Write("dc_drop_watch_end",w.eventId,w.uid,0,0,"2s native deferred loot observation complete");drops.RemoveAt(i);}}}
 static void DropLoot(Hook_Mob.orig_dropLoot orig,Mob self){var instance=Instance;if(instance!=null){var known=new HashSet<int>();var level=self._level;for(int n=0;n<level.entities.length;n++){var e=level.entities.getDyn(n) as Entity;if(e!=null)known.Add(e.__uid);}instance.drops.Add(new DropWatch{uid=self.__uid,eventId=currentGuestEvent,until=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency+2,x=self.cx+self.xr,y=self.cy+self.yr,level=level,known=known});instance.Write("dc_mob_drop",currentGuestEvent,currentGuestEvent>0?CarrierGuard.Hero?.__uid??0:0,self.__uid,self.loots?.length??0,"Native Mob.dropLoot; deferred observation scheduled");}orig(self);}
 public void SuspendForSwitch(){var h=CarrierGuard.Hero;if(h!=null&&CarrierGuard.Active)foreach(var pair in pending){Write("mode_cancel_pending",pair.Key,pair.Value.source?.__uid??0,h.__uid,0,"Switch before native decision; native Miss continuation; no HP applied");ApplyNativeWorldResult(pair.Key,pair.Value,h,"native_filtered",0);}pending.Clear();pendingSince.Clear();incoming.Clear();bullets.Clear();traps.Clear();enabled=false;}
 public void Dispose(){if(disposed)return;disposed=true;enabled=false;Hook_Mob.onDie-=OnDie;Hook_Mob.dropLoot-=DropLoot;Hook_Bullet.onTouchValidTarget-=BulletTouch;Hook_Bullet.fixedUpdate-=BulletFixed;dc.en.ltrap.Hook_Spikes.onTouch-=TrapTouch;traps.Clear();bullets.Clear();slot.Dispose();log.Dispose();Instance=null;}
}
