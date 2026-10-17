using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace EarlyPlaytest {
// Forward the configured PC keys at the original native keyboard polling boundary.
// The original Joystick.UpdateGameInput still owns every action/chord/edge.
public sealed class BridgeKeyboard:MonoBehaviour {
 public static int Mask;public static bool NativeMode;float next;string last="";
 public static bool NativeKey(KeyCode code){
  if(!BridgeArena.Active)return UnityEngine.Input.GetKey(code);
  if(!NativeMode||code==KeyCode.None)return false;
  foreach(PCInput.PCKeyType type in Enum.GetValues(typeof(PCInput.PCKeyType)))
   if(R.Input.GetCode(type)==code&&(Mask&(1<<(int)type))!=0)return true;
  return false;
 }
 void Update(){if(Time.realtimeSinceStartup<next||InputAction.Instance?.wire==null||!InputAction.Instance.wire.Connected||R.Player.gameObject==null)return;next=Time.realtimeSinceStartup+2;
  try{var rows=new List<string>();var file=new List<string>();file.Add("action,typeId,unityKeyCode,windowsVirtualKey,key");
   foreach(PCInput.PCKeyType type in Enum.GetValues(typeof(PCInput.PCKeyType))){KeyCode code=R.Input.GetCode(type);int vk=VirtualKey(code);if(vk==0||vk==0x76)throw new InvalidOperationException("Unsupported or F7-reserved native binding: "+type+"="+code);rows.Add((int)type+":"+vk+":"+code);file.Add(type+","+(int)type+","+(int)code+","+vk+","+code);}
   string value=string.Join(",",rows.ToArray());InputAction.Instance.wire.Send("EP4|KEYMAP|"+GuestCombat.Contract+"|"+rows.Count+"|"+value);
   if(last!=value){last=value;File.WriteAllLines(Path.Combine(InputAction.Out,"native_keymap.csv"),file.ToArray());InputAction.Instance.Event("NATIVE_PC_KEYMAP "+value);}
  }catch(Exception ex){InputAction.Instance.Event("FAIL_NATIVE_KEYMAP "+ex.Message);}}
 static int VirtualKey(KeyCode code){int c=(int)code;if(c>=97&&c<=122)return c-32;if(c>=48&&c<=57)return c;if(c>=282&&c<=296)return 0x70+c-282;
  switch(code){case KeyCode.Space:return 32;case KeyCode.Return:return 13;case KeyCode.Tab:return 9;case KeyCode.Escape:return 27;case KeyCode.Backspace:return 8;
   case KeyCode.UpArrow:return 38;case KeyCode.DownArrow:return 40;case KeyCode.LeftArrow:return 37;case KeyCode.RightArrow:return 39;
   case KeyCode.LeftShift:return 160;case KeyCode.RightShift:return 161;case KeyCode.LeftControl:return 162;case KeyCode.RightControl:return 163;case KeyCode.LeftAlt:return 164;case KeyCode.RightAlt:return 165;
   case KeyCode.Mouse0:return 1;case KeyCode.Mouse1:return 2;case KeyCode.Mouse2:return 4;case KeyCode.Mouse3:return 5;case KeyCode.Mouse4:return 6;
   case KeyCode.Minus:return 189;case KeyCode.Equals:return 187;case KeyCode.LeftBracket:return 219;case KeyCode.RightBracket:return 221;case KeyCode.Backslash:return 220;case KeyCode.Semicolon:return 186;case KeyCode.Quote:return 222;case KeyCode.Comma:return 188;case KeyCode.Period:return 190;case KeyCode.Slash:return 191;case KeyCode.BackQuote:return 192;
   default:return 0;}}
}
[HarmonyPatch(typeof(GameWorld.Joystick),"UpdateGameInput")]
static class NativePcPolling {
 [HarmonyTranspiler]static IEnumerable<CodeInstruction> Forward(IEnumerable<CodeInstruction> instructions){MethodInfo original=AccessTools.Method(typeof(UnityEngine.Input),"GetKey",new Type[]{typeof(KeyCode)}),replacement=AccessTools.Method(typeof(BridgeKeyboard),"NativeKey");int count=0;foreach(var code in instructions){if(code.Calls(original)){code.operand=replacement;count++;}yield return code;}if(count!=15)throw new InvalidOperationException("Native PC polling layout changed: expected 15 key polls; actual "+count);}
}
}
