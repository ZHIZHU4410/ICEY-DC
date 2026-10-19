using System;
using EarlyPlaytest;
int count=0;
void Check(bool value){if(!value)throw new Exception("Failed "+count);count++;}
// Replay observed startup values: native collider was 2 Unity units high in both sessions.
Check(StandingReference.TryHeight(2.25,out var normal)&&normal==54);
double previousGoodScale=2/(54/24.0),previousBadScale=2/(24/24.0),fixedScale=2/(normal/24.0);
Check(Math.Abs(fixedScale-previousGoodScale)<1e-12);
Check(Math.Abs(previousGoodScale/previousBadScale-24/54.0)<1e-12);
Check(!StandingReference.TryHeight(0,out _));Check(!StandingReference.TryHeight(-1,out _));
Check(!StandingReference.TryHeight(double.NaN,out _));Check(!StandingReference.TryHeight(double.PositiveInfinity,out _));
Check(StandingReference.TryHeight(3,out normal)&&normal==72);
Console.WriteLine("Passed "+count+" standing-reference / recorded-scale regressions");
