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
        protected override void ProcessBatch(List<TexeraTuple> batch, ref List<TexeraTuple> output)
        {
           output.Add(new TexeraTuple(-1,new string[]{batch.Count.ToString()}));
        }
    }
}