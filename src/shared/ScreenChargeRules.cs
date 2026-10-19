using System;
namespace EarlyPlaytest {
public static class ScreenChargeRules {
 public const int Width=512,Height=256,Capacity=320+Width*Height*4;
 public const string Mailbox=@"Local\IceyDCBridge_SCREEN_CHARGE_109";
 public static bool IsRelease(string name){return name!=null&&(name.StartsWith("Charge1End",StringComparison.Ordinal)||name.StartsWith("QTECharge1End",StringComparison.Ordinal)||name.StartsWith("AirChargeEnd",StringComparison.Ordinal));}
 public static bool InViewport(double x,double foot,double radius,double height,double viewX,double viewY,double width,double viewHeight){return width>0&&viewHeight>0&&x+radius>=viewX-width*.5&&x-radius<=viewX+width*.5&&foot>=viewY-viewHeight*.5&&foot-height<=viewY+viewHeight*.5;}
}
}
