using System;
using System.Text;

namespace AdvancedK9.Medical
{
    // Compatibility fallback for the specific Nexus dispatch messages observed
    // in RPH. Reads fresh complete lines only; transcripts/AI responses are not commands.
    internal sealed class NexusSceneClearSignal
    {
        private readonly StringBuilder _partial=new StringBuilder();
        internal bool Feed(string chunk)
        {
            bool found=false;
            foreach(char c in chunk??"")
            {
                if(c=='\n')
                {
                    string line=_partial.ToString().TrimEnd('\r');_partial.Clear();
                    int source=line.IndexOf("LSPD First Response: NexusDispatch: ",StringComparison.Ordinal);
                    if(source<0)continue;
                    string message=line.Substring(source+"LSPD First Response: NexusDispatch: ".Length);
                    found |= message=="[DispatchPipeline] Firing action: code4" ||
                        message.StartsWith("[BackupLifecycle] Nexus requested global backup dismissal: Code4;",StringComparison.Ordinal);
                }
                else if(_partial.Length<16384)_partial.Append(c);
                else _partial.Clear();
            }
            return found;
        }
        internal void Reset()=>_partial.Clear();
    }
}
