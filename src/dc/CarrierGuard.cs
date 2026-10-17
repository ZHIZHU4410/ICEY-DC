using System;
using System.IO;
using System.Diagnostics;
using dc;
using dc.en;
using dc.tool.atk;
using dc.tool.hero;
namespace EarlyPlaytest;
// Retain the real Hero identity/world update; suppress its player-owned combat only.
public sealed class CarrierGuard:IDisposable {
 public static CarrierGuard Instance;
 [ThreadStatic]public static int GuestHitDepth;
 readonly StreamWriter log;readonly string output;bool disposed,probed;long outgoing,incoming,damage,skills;double next;Mob fixture;string fixtureRequest="";
 public CarrierGuard(string output){this.output=output;Instance=this;log=new StreamWriter(Path.Combine(output,"carrier_guard.csv"));log.AutoFlush=true;log.WriteLine("utc,qpc,generation,heroUid,event,sourceUid,targetUid,heroLife,targetLife,outgoingBlocked,incomingIntercepted,damageBlocked,skillUpdatesBlocked,result");
  Hook__AttackUtils.hit+=Hit;Hook_Hero.applyAttackResult+=HeroDamage;Hook_Entity.applyAttackResult+=BaseDamage;Hook_Hero.bump+=HeroBump;Hook_Entity.bump+=BaseBump;
  Hook_HeroWeaponsManager.fixedUpdate+=WeaponFixed;Hook_HeroWeaponsManager.postUpdate+=WeaponPost;
  Hook_HeroMainSkillsManager.fixedUpdate+=MainFixed;Hook_HeroMainSkillsManager.postUpdate+=MainPost;
  Hook_HeroActiveSkillsManager.fixedUpdate+=ActiveFixed;Write("installed",null,null,"Awaiting carrier activation; no combat forwarding yet");
 }
 public static Hero Hero=>HostWorld.Instance?.Hero;
 public static bool Active=>Instance!=null&&HostWorld.Instance!=null&&HostWorld.Instance.Active;
 void Write(string kind,Entity source,Entity target,string result){var h=Hero;log.WriteLine(string.Join(",",DateTime.UtcNow.ToString("o"),Stopwatch.GetTimestamp(),HostWorld.Instance?.Generation??0,h?.__uid??0,kind,source?.__uid??0,target?.__uid??0,h?.life??0,target?.life??0,outgoing,incoming,damage,skills,result.Replace(',',';')));}
 static bool IsHero(Entity e)=>e!=null&&e==Hero;
 static void Hit(Hook__AttackUtils.orig_hit orig,AttackData a,Entity target){
  if(!Active){orig(a,target);return;}if(HostCombat.ApplyAcknowledgedBullet(a,target))return;
  if(IsHero(a.source)||IsHero(a.carrier)){if(GuestHitDepth>0){orig(a,target);return;}Instance.outgoing++;a.hitResult=new dc.HitResult.Miss();Instance.Write("blocked_dc_outgoing",a.source,target,"No DC native attack result applied");return;}
  if(IsHero(target)){Instance.incoming++;HostCombat.Instance?.Incoming(a,target,"AttackUtils.hit before DC player defense");a.hitResult=new dc.HitResult.Miss();Instance.Write("intercept_dc_incoming",a.source,target,HostCombat.Enabled?"Forwarded to native guest event channel":"Preflight quarantine; guest event channel not enabled");return;}
  orig(a,target);
 }
 static void HeroDamage(Hook_Hero.orig_applyAttackResult orig,Hero self,AttackData a){if(Active&&IsHero(self)){Instance.damage++;HostCombat.Instance?.Incoming(a,self,"Direct Hero.applyAttackResult fallback");Instance.Write("blocked_hero_damage",a.source,self,"Original Hero applyAttackResult skipped");return;}orig(self,a);}
 static void BaseDamage(Hook_Entity.orig_applyAttackResult orig,Entity self,AttackData a){if(Active&&IsHero(self)){Instance.damage++;HostCombat.Instance?.Incoming(a,self,"Direct Entity.applyAttackResult fallback");Instance.Write("blocked_base_damage",a.source,self,"Original Entity HP/death pipeline skipped");return;}orig(self,a);}
 static void HeroBump(Hook_Hero.orig_bump orig,Hero self,double x,double y,bool? forced){if(Active&&IsHero(self)){Instance.Write("blocked_dc_bump",null,self,"ICEY owns hurt movement; x="+x+" y="+y);return;}orig(self,x,y,forced);}
 static void BaseBump(Hook_Entity.orig_bump orig,Entity self,double x,double y,bool? forced){if(Active&&IsHero(self)){Instance.Write("blocked_dc_base_bump",null,self,"ICEY owns hurt movement; x="+x+" y="+y);return;}orig(self,x,y,forced);}
 static void WeaponFixed(Hook_HeroWeaponsManager.orig_fixedUpdate orig,HeroWeaponsManager self){if(Active&&self==Hero.weaponsManager){Instance.skills++;return;}orig(self);}
 static void WeaponPost(Hook_HeroWeaponsManager.orig_postUpdate orig,HeroWeaponsManager self){if(Active&&self==Hero.weaponsManager){Instance.skills++;return;}orig(self);}
 static void MainFixed(Hook_HeroMainSkillsManager.orig_fixedUpdate orig,HeroMainSkillsManager self){if(Active&&self==Hero.mainSkillsManager){Instance.skills++;return;}orig(self);}
 static void MainPost(Hook_HeroMainSkillsManager.orig_postUpdate orig,HeroMainSkillsManager self){if(Active&&self==Hero.mainSkillsManager){Instance.skills++;return;}orig(self);}
 static void ActiveFixed(Hook_HeroActiveSkillsManager.orig_fixedUpdate orig,HeroActiveSkillsManager self){if(Active&&self==Hero.activeSkillsManager){Instance.skills++;return;}orig(self);}
 public void Update(){if(disposed||!Active)return;double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;if(now<next)return;next=now+.25;Write("observe",fixture,Hero,fixture==null||fixture.destroyed?"Carrier identity/world update retained":"MobX="+(fixture.cx+fixture.xr)+" MobY="+(fixture.cy+fixture.yr)+" HeroX="+(Hero.cx+Hero.xr)+" HeroY="+(Hero.cy+Hero.yr)+" AiTarget="+(fixture.aTarget?.__uid??0));if(!probed&&File.Exists(Path.Combine(output,"carrier_probe.flag"))){probed=true;Probe();}string requestPath=Path.Combine(output,"fixture_request.txt");if(File.Exists(requestPath)){string request=File.ReadAllText(requestPath).Trim();if(request!=fixtureRequest){fixtureRequest=request;var fields=request.Split('|');if(fields.Length!=3||File.Exists(Path.Combine(output,"defense.flag"))){Write("fixture_request_rejected",null,Hero,"Require unique id|Zombie/Archer|tier with defense gate closed");return;}int tier=int.Parse(fields[2]);if(tier<1||tier>12){Write("fixture_request_rejected",null,Hero,"Tier outside 1..12");return;}if(fixture!=null&&!fixture.destroyed&&fixture.life>0){Write("fixture_request_rejected",fixture,Hero,"Previous fixture still alive; never silently remove it");return;}Mob mob=fields[1]=="Zombie"?dc.en.mob.Zombie.Class.create.Invoke(Hero._level,Hero.cx+3,Hero.cy,1,tier):fields[1]=="Archer"?dc.en.mob.Archer.Class.create.Invoke(Hero._level,Hero.cx+7,Hero.cy,1,tier):null;if(mob==null){Write("fixture_request_rejected",null,Hero,"Unsupported kind");return;}fixture=mob;mob.dir=-1;mob.addMoneyToLoot(10,true);Write("fixture_spawn",Hero,mob,request+" native create/init; natural AI and native loot10");}}}
 void Probe(){var h=Hero;int tier=1;string file=Path.Combine(output,"fixture_life_tier.txt");if(File.Exists(file))tier=int.Parse(File.ReadAllText(file).Trim());if(tier<1||tier>12)throw new InvalidOperationException("Fixture life tier outside 1..12");var m=dc.en.mob.Zombie.Class.create.Invoke(h._level,h.cx+3,h.cy,1,tier);fixture=m;m.dir=-1;m.addMoneyToLoot(10,true);Write("probe_spawn_native_zombie",h,m,"Native create+init; lifeTier="+tier+" native gold loot=10; AI remains original");int hl=h.life,ml=m.life;
  var outgoingAttack=AttackUtils.Class.createFromHero.Invoke(h,(object)10.0,null);AttackUtils.Class.hit.Invoke(outgoingAttack,m);
  Write("probe_outgoing_result",h,m,m.life==ml?"blocked_no_mob_damage":"FAIL_mob_life_changed");
  var incomingAttack=AttackUtils.Class.createFromMob.Invoke(m,(object)10.0,null);AttackUtils.Class.hit.Invoke(incomingAttack,h);h.applyAttackResult(incomingAttack);
  Write("probe_incoming_result",m,h,h.life==hl?"blocked_no_hero_damage":"FAIL_hero_life_changed");
 }
 public void Dispose(){if(disposed)return;disposed=true;Write("stop",null,null,"Native hooks removed after tests");Hook__AttackUtils.hit-=Hit;Hook_Hero.applyAttackResult-=HeroDamage;Hook_Entity.applyAttackResult-=BaseDamage;Hook_Hero.bump-=HeroBump;Hook_Entity.bump-=BaseBump;Hook_HeroWeaponsManager.fixedUpdate-=WeaponFixed;Hook_HeroWeaponsManager.postUpdate-=WeaponPost;Hook_HeroMainSkillsManager.fixedUpdate-=MainFixed;Hook_HeroMainSkillsManager.postUpdate-=MainPost;Hook_HeroActiveSkillsManager.fixedUpdate-=ActiveFixed;log.Dispose();Instance=null;}
}
