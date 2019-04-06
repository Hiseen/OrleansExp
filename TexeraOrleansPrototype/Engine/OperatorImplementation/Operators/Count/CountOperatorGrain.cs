using Orleans;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Orleans.Concurrency;
using Engine.OperatorImplementation.MessagingSemantics;
using Engine.OperatorImplementation.Common;
using TexeraUtilities;

namespace Engine.OperatorImplementation.Operators
{

    public class CountOperatorGrain : WorkerGrain, ICountOperatorGrain
    {
        protected override int BatchingLimit {get{return 1;}}
        protected override void ProcessBatch(List<TexeraTuple> batch,ref List<TexeraTuple> output)
        {
           output.Add(new TexeraTuple(-1,null,batch.Count));
        }
    }
}