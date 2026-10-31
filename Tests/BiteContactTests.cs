using System;
using AdvancedK9;

internal static class BiteContactTests
{
    private static readonly BitePoint Lower=new BitePoint(0,0,-.8f),Upper=new BitePoint(0,0,0);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static bool Contact(BiteContactWindow w,uint t,float x,float y,float root=1.46f,float height=.6f,bool eligible=true,bool los=true)
        =>w.TryCommit(t,new BitePoint(x,y,-.3f),Lower,Upper,root,height,eligible,los);
    public static void Main()
    {
        // First-pass root spacing from the test log must not veto a muzzle that
        // already reaches the body's contact envelope.
        var first=new BiteContactWindow();
        Check(Contact(first,0,0,.5f),"Muzzle reaches body before the old 1.15m root gate");
        Check(!Contact(first,16,0,.4f),"A committed bite cannot retrigger");
        Check(!Contact(new BiteContactWindow(),0,0,1f,1.9f),"Root proximity alone cannot cause a remote bite");
        Check(Contact(new BiteContactWindow(),0,.5f,0),"Side contact must work");
        Check(Contact(new BiteContactWindow(),0,0,-.5f),"Rear approach must work");
        var sweep=new BiteContactWindow();
        Check(!Contact(sweep,0,-.7f,0),"Pre-contact sample must continue pursuit");
        Check(Contact(sweep,50,.7f,0),"Fast crossing between frames must commit on first pass");
        var hitch=new BiteContactWindow();Contact(hitch,0,-.7f,0);
        Check(!Contact(hitch,200,.7f,0),"Stale movement must not create a bite");
        var teleport=new BiteContactWindow();Contact(teleport,0,-2f,0);
        Check(!Contact(teleport,16,2f,0),"Teleport across the capsule must not create a bite");
        Check(!Contact(new BiteContactWindow(),0,0,.1f,2.1f),"Excess root spacing must be rejected");
        Check(!Contact(new BiteContactWindow(),0,0,.1f,1.4f,1.1f),"Different floor/height must be rejected");
        Check(!Contact(new BiteContactWindow(),0,0,.1f,1.4f,.6f,false),"Ineligible target must be rejected");
        Check(!Contact(new BiteContactWindow(),0,0,.1f,1.4f,.6f,true,false),"Obstructed contact must be rejected");
        var blocked=new BiteContactWindow();Contact(blocked,0,-.7f,0,1.4f,.6f,true,false);
        Check(!Contact(blocked,16,.7f,0),"An obstructed old sample must not sweep through a barrier");
        var q=new BitePoint(0,0,0);
        Check(BiteContactWindow.SegmentDistanceSquared(q,q,q,q)==0,"Degenerate point segments");
        Check(Math.Abs(BiteContactWindow.SegmentDistanceSquared(new BitePoint(1,0,0),new BitePoint(1,0,1),new BitePoint(0,0,0),new BitePoint(0,0,1))-1)<.0001,"Parallel segments");
        Console.WriteLine("Bite contact regressions passed: first pass, swept contact, one-shot commit, range/height/obstruction and stale-sample rejection.");
    }
}
