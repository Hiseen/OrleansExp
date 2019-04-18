using Orleans;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Orleans.Concurrency;
using TexeraUtilities;
using Engine.OperatorImplementation.Common;

namespace Engine.OperatorImplementation.Operators
{

    public class CountFinalOperatorGrain : WorkerGrain, ICountFinalOperatorGrain
    {
        protected override bool WorkAsExternalTask {get{return true;}}
        public int count = 0;
        protected override void ProcessTuple(TexeraTuple tuple)
        {
            count+=int.Parse(tuple.FieldList[0]);
        }

        protected override void MakeFinalOutputTuples()
        {
            outputTuples.Add(new TexeraTuple(-1,new string[]{count.ToString()}));
        }
    }

}