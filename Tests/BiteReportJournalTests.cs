using System;
using System.IO;
using System.Linq;
using AdvancedK9.Mdt;

internal static class BiteReportJournalTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Main()
    {
        string folder=Path.Combine(Path.GetTempPath(),"ak9-mdt-test-"+Guid.NewGuid().ToString("N"));
        try
        {
            string id=Guid.NewGuid().ToString("N"),other=Guid.NewGuid().ToString("N");
            var journal=new BiteReportJournal(folder);
            var bite=new BiteReportEvent{Episode=id,Stage="Bite",Utc=DateTime.UtcNow,K9="Rex\n=Test",PedHandle="123",Model="a_c_shepherd",Detail="Bite confirmed",Position="test"};
            Check(journal.Add(bite),"First contact must be stored");
            bite.Detail="Do not overwrite";Check(!journal.Add(bite),"Repeated bite callback must not duplicate or replace the event");
            journal.SetEnabled(false);Check(!new BiteReportJournal(folder).Enabled,"Disabled sharing must persist across the bridge boundary");
            journal.SetEnabled(true);Check(new BiteReportJournal(folder).Enabled,"Re-enabled sharing resumes pending notes");
            var restart=new BiteReportJournal(folder);var e=restart.Events().Single();
            var culture=System.Globalization.CultureInfo.CurrentCulture;
            try{System.Globalization.CultureInfo.CurrentCulture=new System.Globalization.CultureInfo("th-TH");Check(restart.Events().Single().Utc.Year==bite.Utc.Year,"UTC snapshots must remain stable across language/locale settings");}
            finally{System.Globalization.CultureInfo.CurrentCulture=culture;}
            Check(e.K9=="Rex\n=Test"&&e.Detail=="Bite confirmed","Snapshot and escaped fields must survive restart");
            Check(restart.Report(id)=="","No guessed report binding");
            restart.MarkSeen(id);Check(new BiteReportJournal(folder).Seen(id),"An old unresolved episode must retain its automatic-link attempt marker");
            Check(restart.Bind(id,"A-2026-0001"),"Explicit report binding");
            Check(!restart.Bind(id,"A-2026-0002")&&restart.Report(id)=="A-2026-0001","Report changes must not split an existing bite across incidents");
            Check(!restart.Bind(other,"bad\nnumber")&&!restart.Bind(other,""),"Invalid bindings must be rejected");
            Check(restart.Bind(other,"A-2026-0002")&&restart.Report(id)=="A-2026-0001","Another bite must keep an independent report binding");
            Check(BiteReportJournal.BeginDelivery(e),"First delivery acquires the marker");
            Check(!BiteReportJournal.BeginDelivery(e),"Ambiguous delivery must not auto-retry");
            Check(BiteReportJournal.Uncertain(new BiteReportJournal(folder).Events().Single()),"Crash uncertainty must survive restart");
            restart.RetryUncertain(id);Check(BiteReportJournal.BeginDelivery(e),"Explicit review/retry can release an unconfirmed attempt");
            BiteReportJournal.Accept(e,"A-2026-0001");
            restart.RetryUncertain(id);Check(BiteReportJournal.Accepted(e)&&!BiteReportJournal.BeginDelivery(e),"Accepted notes must never be resent by retry");
            var treatment=new BiteReportEvent{Episode=id,Stage="TreatmentCompleted",Utc=DateTime.UtcNow,K9="Rex",PedHandle="123",Model="a_c_shepherd",Detail="Observed completion",Position="test"};
            Check(restart.Add(treatment),"Treatment is a separate milestone from the bite");
            Check(restart.Events().Count()==2&&restart.Events().Count(BiteReportJournal.Accepted)==1,"Bite receipt must not discard a pending treatment event");
            var pending=restart.Events().Single(x=>x.Stage=="TreatmentCompleted");
            BiteReportJournal.BeginDelivery(pending);BiteReportJournal.Reject(pending);
            Check(!BiteReportJournal.Accepted(pending)&&!BiteReportJournal.Uncertain(pending),"Explicit provider rejection must remain pending");
            Console.WriteLine("Bite/EMS journal regression checks passed: persistence, independent immutable binding, milestone deduplication and ambiguous-delivery handling.");
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
