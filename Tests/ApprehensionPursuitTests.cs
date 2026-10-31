using System;
using AdvancedK9;

internal static class ApprehensionPursuitTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void MovingChase(float dx,float dy,string scenario)
    {
        var chase=new ApprehensionPursuitProgress(0,0,0);
        // Runs beyond the former 25-second recall. Increasing separation from
        // a faster suspect and taking a sideways detour must not reset movement.
        for(uint t=500;t<=90000;t+=500)
            Check(chase.Observe(t,t/500*dx,t/500*dy,1,false)==PursuitRecovery.None,scenario+" interrupted at "+t);
    }
    public static void Main()
    {
        MovingChase(2,0,"Running after a faster target");
        MovingChase(0,1,"Sideways obstacle detour");
        MovingChase(-1,0,"Target turns behind the dog");
        MovingChase(.15f,0,"Slow but continuing navigation");
        var stuck=new ApprehensionPursuitProgress(0,0,0);
        Check(stuck.Observe(1000,0,0,1,false)==PursuitRecovery.None,"Startup must not be restarted after one second");
        Check(stuck.Observe(2500,0,0,1,false)==PursuitRecovery.None,"Short pause must retain route");
        Check(stuck.Observe(3000,0,0,1,false)==PursuitRecovery.Stalled,"A genuinely stationary dog needs recovery");
        stuck.Assigned(3000,0,0);
        Check(stuck.Observe(3500,0,0,1,false)==PursuitRecovery.None,"Recovery must allow acceleration");
        var ended=new ApprehensionPursuitProgress(0,0,0);
        Check(ended.Observe(2000,2,0,7,false)==PursuitRecovery.None,"One transient status must not reset pursuit");
        Check(ended.Observe(2500,3,0,1,false)==PursuitRecovery.None,"Recovered native status must cancel failure timer");
        ended.Observe(3000,4,0,7,false);
        Check(ended.Observe(3500,4,0,7,false)==PursuitRecovery.TaskEnded,"Sustained missing/finished task must recover");
        var jumping=new ApprehensionPursuitProgress(0,0,0);
        Check(jumping.Observe(4000,0,0,7,true)==PursuitRecovery.None,"Physical transitions must finish");
        Check(jumping.Observe(4500,0,0,1,false)==PursuitRecovery.None,"Landing must receive acceleration time");
        var wrap=new ApprehensionPursuitProgress(uint.MaxValue-500,0,0);
        Check(wrap.Observe(499,0,0,1,false)==PursuitRecovery.None,"Clock wrap must not create a stall");
        Console.WriteLine("Continuous pursuit regression replays passed.");
    }
}
