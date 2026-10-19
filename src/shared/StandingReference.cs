using System;
namespace EarlyPlaytest {
// The host's bodyHei is in cells. Entity.hei is a transient collision box in pixels.
public static class StandingReference {
 public static bool TryHeight(double bodyHeightCells,out double pixels){pixels=bodyHeightCells*24.0;return !double.IsNaN(pixels)&&!double.IsInfinity(pixels)&&pixels>0;}
}
}
