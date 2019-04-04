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
using System.Linq;

namespace Engine.OperatorImplementation.Operators
{
    public class JoinOperatorGrain : WorkerGrain, IJoinOperatorGrain
    {
        // protected override bool WorkAsExternalTask {get{return true;}}
        Dictionary<int,List<TexeraTuple>> joinedTuples=new Dictionary<int, List<TexeraTuple>>();
        int TableID;
        public override Task Init(IWorkerGrain self, PredicateBase predicate, IPrincipalGrain principalGrain)
        {
            base.Init(self,predicate,principalGrain);
            TableID=((JoinPredicate)predicate).TableID;
            return Task.CompletedTask;
        }
        protected override List<TexeraTuple> ProcessTuple(TexeraTuple tuple)
        {
            List<TexeraTuple> result=new List<TexeraTuple>();
            foreach(KeyValuePair<int,List<TexeraTuple>> entry in joinedTuples)
            {
                if(entry.Key!=tuple.TableID)
                {
                    foreach(TexeraTuple t in entry.Value)
                    {
                        result.Add(new TexeraTuple(TableID,tuple.FieldList.Concat(t.FieldList).ToArray()));
                    }
                }
            }
            if(joinedTuples.ContainsKey(tuple.TableID))
            {
                joinedTuples[tuple.TableID].Add(tuple);
            }
            else
            {
                joinedTuples.Add(tuple.TableID,new List<TexeraTuple>{tuple});
            }
            return result;
        }
    }

}