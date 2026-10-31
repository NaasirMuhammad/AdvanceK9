using System;

namespace AdvancedK9
{
    internal enum PursuitRecovery { None, TaskEnded, Stalled }

    // Measures the dog's own movement. A faster or turning target is not evidence
    // that a live navigation task failed. No elapsed chase deadline is imposed.
    internal sealed class ApprehensionPursuitProgress
    {
        private uint _assignedAt,_lastMovementAt,_taskEndedAt;
        private bool _taskEnded;
        private float _anchorX,_anchorY;

        internal ApprehensionPursuitProgress(uint now,float x,float y){Assigned(now,x,y);}

        internal void Assigned(uint now,float x,float y)
        {
            _assignedAt=_lastMovementAt=now;_anchorX=x;_anchorY=y;
            _taskEnded=false;_taskEndedAt=0;
        }

        internal PursuitRecovery Observe(uint now,float x,float y,int taskStatus,bool physicalTransition)
        {
            if(physicalTransition)
            {
                // A jump, ragdoll or get-up owns movement until it finishes.
                Assigned(now,x,y);return PursuitRecovery.None;
            }
            float dx=x-_anchorX,dy=y-_anchorY;
            if(dx*dx+dy*dy>=.36f)
            {
                _anchorX=x;_anchorY=y;_lastMovementAt=now;
            }
            if(taskStatus==3||taskStatus==7)
            {
                if(!_taskEnded){_taskEnded=true;_taskEndedAt=now;}
            }
            else _taskEnded=false;
            if(unchecked(now-_assignedAt)<1500)return PursuitRecovery.None;
            if(_taskEnded&&unchecked(now-_taskEndedAt)>=500)return PursuitRecovery.TaskEnded;
            if(unchecked(now-_lastMovementAt)>=3000)return PursuitRecovery.Stalled;
            return PursuitRecovery.None;
        }
    }
}
