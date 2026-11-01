using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;
using AdvancedK9.API;

namespace AdvancedK9
{
    internal sealed partial class K9Controller
    {
        private sealed class SuspectSession
        {
            internal Runtime.PedIdentity Identity;
            internal readonly SuspectControlState State=new SuspectControlState();
            internal readonly string Episode=Guid.NewGuid().ToString("N");
            internal uint NextPublish,FinalAt;
        }
        private readonly Dictionary<Ped,SuspectSession> _suspectSessions=new Dictionary<Ped,SuspectSession>();

        private bool BeginSuspectSession(Ped ped)
        {
            Runtime.PedIdentity id;if(!Runtime.PedIdentity.TryCapture(ped,out id))return false;
            SuspectSession existing;
            if(_suspectSessions.TryGetValue(ped,out existing)&&existing.Identity.Matches(ped)&&existing.State.ProviderOwns)return false;
            _suspectSessions[ped]=new SuspectSession{Identity=id};
            PublishSuspectLease(_suspectSessions[ped]);
            Game.LogTrivial("AdvancedK9 suspect ownership: ped="+id.Handle+" acquired Chasing; episode="+_suspectSessions[ped].Episode+". Cooperating callout outcome tasking suspended for this ped only.");
            return true;
        }

        private bool ValidSuspect(Ped ped)
        {
            SuspectSession session;
            if(ped==null||!_suspectSessions.TryGetValue(ped,out session))return false;
            return session.Identity.Matches(ped)&&session.State.Phase!=SuspectControlPhase.Unavailable;
        }
        private bool ProviderOwnsSuspect(Ped ped)
        {
            SuspectSession session;return ped!=null&&_suspectSessions.TryGetValue(ped,out session)&&session.State.ProviderOwns;
        }
        private bool SuspectPoseAllowed(Ped ped)
        {
            SuspectSession session;return ValidSuspect(ped)&&_suspectSessions.TryGetValue(ped,out session)&&session.State.CanWritePose;
        }
        private void SetSuspectPhase(Ped ped,SuspectControlPhase next,string reason)
        {
            SuspectSession session;if(ped==null||!_suspectSessions.TryGetValue(ped,out session))return;
            var before=session.State.Phase;if(!session.State.Set(next))return;
            if(next==SuspectControlPhase.Released||next==SuspectControlPhase.Cuffed||next==SuspectControlPhase.Unavailable)session.FinalAt=Game.GameTime;
            session.NextPublish=0;PublishSuspectLease(session);
            string observation="identity unavailable";
            if(session.Identity.Matches(ped))try{observation="health="+ped.Health+", restrained="+IsTargetDurablyRestrained(ped);}catch{}
            Game.LogTrivial("AdvancedK9 suspect ownership: ped="+session.Identity.Handle+", episode="+session.Episode+", "+before+" -> "+session.State.Phase+"; reason="+reason+"; "+observation+".");
        }
        private static void PublishSuspectLease(SuspectSession session)
        {
            try{SuspectControlApi.Publish(new SuspectControlLease{Handle=session.Identity.Handle,Model=session.Identity.Model,Address=session.Identity.Address,Episode=session.Episode,Phase=session.State.Phase});}
            catch(Exception ex){Game.LogTrivial("AdvancedK9 suspect lease write contained: "+ex.Message);}
            session.NextPublish=Game.GameTime+500;
        }

        private void RetireSuspectSession(Ped ped,string reason)
        {
            if(ped==null)return;
            if(ValidSuspect(ped))
            {
                try{RestoreBitePatientControl(ped);_pr.ReleaseK9Custody(ped);}
                catch(Exception ex){Game.LogTrivial("AdvancedK9 suspect retirement: safe control restoration unavailable: "+ex.GetType().Name);}
            }
            SetSuspectPhase(ped,SuspectControlPhase.Unavailable,reason);
            _bitePatientEventStates.Remove(ped);_suspectSessions.Remove(ped);
            if(_releasedBiteTarget==ped){_releasedBiteTarget=null;_releasedBiteFrozen=false;}
            if(_controlledBiteTarget==ped){_controlledBiteTarget=null;_controlledBiteHoldUntil=0;}
        }

        private void MaintainSuspectOwnership()
        {
            foreach(var pair in _suspectSessions.ToArray())
            {
                Ped ped=pair.Key;var session=pair.Value;
                if(!session.Identity.Matches(ped))
                {
                    SetSuspectPhase(ped,SuspectControlPhase.Unavailable,"entity removed/replaced/zero address; stale writes cancelled");
                    _bitePatientEventStates.Remove(ped);
                    if(_releasedBiteTarget==ped){_releasedBiteTarget=null;_releasedBiteFrozen=false;}
                    if(_controlledBiteTarget==ped){_controlledBiteTarget=null;_controlledBiteHoldUntil=0;if(DogExists())Follow();}
                    _suspectSessions.Remove(ped);continue;
                }
                try
                {
                    string provider=_pr.SuspectProviderState(session.Identity.Handle,session.Identity.Model,session.Identity.Address);
                    bool cuffed=IsTargetDurablyRestrained(ped)||provider=="Cuffed";
                    bool arresting=!cuffed&&(provider=="Arresting"||NativeFunction.Natives.IS_PED_BEING_ARRESTED<bool>(ped));
                    bool interacting=provider=="ProviderInteraction";
                    if(cuffed||arresting||interacting)
                    {
                        bool first=!session.State.ProviderOwns;
                        SetSuspectPhase(ped,cuffed?SuspectControlPhase.Cuffed:arresting?SuspectControlPhase.Arresting:SuspectControlPhase.ProviderInteraction,"observed external provider interaction/restraint; yielding suspect tasks");
                        if(first)
                        {
                            RestoreBitePatientControl(ped); // Only our freeze/animation/event toggles; never clear provider tasks.
                            _pr.HandOffK9Custody(ped,cuffed?"Cuffed":"Arresting");
                            if(_controlledBiteTarget==ped){_controlledBiteTarget=null;_controlledBiteHoldUntil=0;if(DogExists())Follow();}
                            if(_releasedBiteTarget==ped)_releasedBiteFrozen=false;
                        }
                        if(cuffed)
                        {
                            _pr.RecordBiteReportEvent(ped,"Cuffed",_profile.Name,"Completed restraint observed; AdvancedK9 yielded suspect tasking to the arrest provider.");
                            if(_releasedBiteTarget==ped&&!_biteMedicalResponseActive&&!_biteMedicalLoadingPatient)ClearReleasedBiteTarget();
                        }
                    }
                    if(Game.GameTime>=session.NextPublish)PublishSuspectLease(session);
                    if(session.FinalAt!=0&&Game.GameTime-session.FinalAt>120000)_suspectSessions.Remove(ped);
                }
                catch(Exception ex)
                {
                    // An invalid-address/native argument failure retires this session;
                    // retrying it every frame risks writing into a replacement entity.
                    RetireSuspectSession(ped,"observation failed: "+ex.GetType().Name+"; session retired");
                }
            }
        }
    }
}
