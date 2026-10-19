using System;
using System.Globalization;
namespace EarlyPlaytest {
// Immutable hit-time origin. A source may die before the newest entity mailbox is read.
public sealed class DamageOrigin {
 public double X,Y,Radius,Height;public int Direction,Tier;
 public string Encode(){return string.Join(";",new[]{"D1",X.ToString("R",CultureInfo.InvariantCulture),Y.ToString("R",CultureInfo.InvariantCulture),Radius.ToString("R",CultureInfo.InvariantCulture),Height.ToString("R",CultureInfo.InvariantCulture),Direction.ToString(CultureInfo.InvariantCulture),Tier.ToString(CultureInfo.InvariantCulture)});}
 public static DamageOrigin Decode(string value){var p=value.Split(';');if(p.Length!=7||p[0]!="D1")throw new FormatException("Unsupported incoming damage origin; update both bridge plugins together");var o=new DamageOrigin{X=double.Parse(p[1],CultureInfo.InvariantCulture),Y=double.Parse(p[2],CultureInfo.InvariantCulture),Radius=double.Parse(p[3],CultureInfo.InvariantCulture),Height=double.Parse(p[4],CultureInfo.InvariantCulture),Direction=int.Parse(p[5],CultureInfo.InvariantCulture),Tier=int.Parse(p[6],CultureInfo.InvariantCulture)};foreach(double n in new[]{o.X,o.Y,o.Radius,o.Height})if(double.IsNaN(n)||double.IsInfinity(n))throw new FormatException("Non-finite damage origin");if(o.Radius<0||o.Height<0||o.Tier<0||o.Tier>2||o.Direction<-1||o.Direction>1)throw new FormatException("Invalid damage origin bounds");return o;}
}
}
