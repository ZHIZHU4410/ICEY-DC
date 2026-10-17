using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
namespace EarlyPlaytest;
public static class BridgeKeys {
 static readonly Dictionary<int,int> keys=new();static string last="";public static bool Ready;
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 public static bool Handle(string[] p){if(p.Length<2||p[1]!="KEYMAP")return false;
  if(p.Length!=5||p[2]!=HostCombat.Contract||p[3]!="15")throw new InvalidOperationException("Native keymap contract mismatch");
  var next=new Dictionary<int,int>();foreach(string item in p[4].Split(',')){var f=item.Split(':');if(f.Length!=3)throw new InvalidOperationException("Bad native keymap");int id=int.Parse(f[0]),vk=int.Parse(f[1]);if(vk<=0||vk>254||vk==0x76||next.ContainsKey(id))throw new InvalidOperationException("Invalid/reserved native key");next.Add(id,vk);}
  int[] expected={0,1,2,3,4,5,6,7,8,10,11,12,13,15,16};if(next.Count!=expected.Length)throw new InvalidOperationException("Incomplete native keymap");foreach(int id in expected)if(!next.ContainsKey(id))throw new InvalidOperationException("Missing native PC action "+id);
  keys.Clear();foreach(var pair in next)keys.Add(pair.Key,pair.Value);Ready=true;if(last!=p[4]){last=p[4];File.WriteAllText(Path.Combine(EpSession.Output,"host_native_keymap.txt"),DateTime.UtcNow.ToString("o")+"\n"+p[4]);HostCombat.Instance?.Write("native_keymap_ready",0,0,0,15,p[4]);}return true;
 }
 public static int Read(bool foreground){if(!foreground||!Ready)return 0;int mask=0;foreach(var p in keys)if((GetAsyncKeyState(p.Value)&0x8000)!=0)mask|=1<<p.Key;return mask;}
}
