using System;

namespace AdvancedK9
{
    internal struct BitePoint
    {
        internal float X,Y,Z;
        internal BitePoint(float x,float y,float z){X=x;Y=y;Z=z;}
        public static BitePoint operator +(BitePoint a,BitePoint b)=>new BitePoint(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static BitePoint operator -(BitePoint a,BitePoint b)=>new BitePoint(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static BitePoint operator *(BitePoint a,float f)=>new BitePoint(a.X*f,a.Y*f,a.Z*f);
        internal static float Dot(BitePoint a,BitePoint b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    }

    // Inputs are relative to the locked suspect, accounting for both actors'
    // movement. No prediction into the future and no enlarged root-distance bite.
    internal sealed class BiteContactWindow
    {
        private bool _sampled,_committed;
        private uint _previousAt;
        private BitePoint _previous;
        internal bool TryCommit(uint now,BitePoint muzzle,BitePoint lowerBody,BitePoint upperBody,
            float horizontalRootGap,float verticalRootGap,bool eligible,bool clearLineOfSight)
        {
            if(_committed)return false;
            if(!eligible||!clearLineOfSight||horizontalRootGap>2f||Math.Abs(verticalRootGap)>=1f)
            {_sampled=false;return false;}
            const float contactRadius=.55f;
            bool contact=SegmentDistanceSquared(muzzle,muzzle,lowerBody,upperBody)<=contactRadius*contactRadius;
            BitePoint motion=muzzle-_previous;
            // Do not turn a hitch, teleport or stale sample into a remote bite.
            if(!contact&&_sampled&&unchecked(now-_previousAt)<=100&&BitePoint.Dot(motion,motion)<=2.25f)
                contact=SegmentDistanceSquared(_previous,muzzle,lowerBody,upperBody)<=contactRadius*contactRadius;
            _sampled=true;_previousAt=now;_previous=muzzle;
            if(contact)_committed=true;
            return contact;
        }

        private static float Clamp(float f)=>Math.Max(0f,Math.Min(1f,f));
        internal static float SegmentDistanceSquared(BitePoint p1,BitePoint q1,BitePoint p2,BitePoint q2)
        {
            BitePoint d1=q1-p1,d2=q2-p2,r=p1-p2;
            float a=BitePoint.Dot(d1,d1),e=BitePoint.Dot(d2,d2),f=BitePoint.Dot(d2,r),s,t;
            if(a<=.000001f&&e<=.000001f)return BitePoint.Dot(r,r);
            if(a<=.000001f){s=0;t=Clamp(f/e);}
            else
            {
                float c=BitePoint.Dot(d1,r);
                if(e<=.000001f){t=0;s=Clamp(-c/a);}
                else
                {
                    float b=BitePoint.Dot(d1,d2),denom=a*e-b*b;
                    s=denom>.000001f?Clamp((b*f-c*e)/denom):0f;
                    t=(b*s+f)/e;
                    if(t<0f){t=0;s=Clamp(-c/a);}
                    else if(t>1f){t=1;s=Clamp((b-c)/a);}
                }
            }
            BitePoint closest=r+d1*s-d2*t;
            return BitePoint.Dot(closest,closest);
        }
    }
}
