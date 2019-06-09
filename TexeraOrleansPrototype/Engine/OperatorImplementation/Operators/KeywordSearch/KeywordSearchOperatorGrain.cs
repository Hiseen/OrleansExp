// #define PRINT_MESSAGE_ON
//#define PRINT_DROPPED_ON


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
using Orleans.Runtime;

namespace Engine.OperatorImplementation.Operators
{
    public class KeywordSearchOperatorGrain : WorkerGrain, IKeywordSearchOperatorGrain
    {
        int searchIndex;
        string keyword;

        public override async Task<SiloAddress> Init(IWorkerGrain self, PredicateBase predicate, IPrincipalGrain principalGrain)
        {
            SiloAddress addr=await base.Init(self,predicate,principalGrain);
            searchIndex=((KeywordPredicate)predicate).SearchIndex;
            keyword=((KeywordPredicate)predicate).Query;
            return addr;
        }


        protected override void ProcessTuple(in TexeraTuple tuple,List<TexeraTuple> output)
        {
            if(tuple.FieldList[searchIndex].Contains(keyword))
                output.Add(tuple);
        }
    }
}