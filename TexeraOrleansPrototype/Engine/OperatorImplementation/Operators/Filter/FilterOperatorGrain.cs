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

namespace Engine.OperatorImplementation.Operators
{
    public class FilterOperatorGrain : NormalGrain, IFilterOperatorGrain
    {
        bool finished = false;
        IOrderingEnforcer orderingEnforcer = Utils.GetOrderingEnforcerInstance();

        public override Task OnActivateAsync()
        {
            // nextGrain = base.GrainFactory.GetGrain<IKeywordSearchOperatorGrain>(this.GetPrimaryKey(), Constants.OperatorAssemblyPathPrefix);//, "KeywordSearchOperatorGrain"
            return base.OnActivateAsync();
        }

        public override async Task Process(Immutable<List<TexeraTuple>> batch)
        {
            string extensionKey = "";
            Console.Write(" Filter received batch ");
            if(batch.Value.Count == 0)
            {
                Console.WriteLine($"NOT EXPECTED: Filter {this.GetPrimaryKey(out extensionKey)} received empty batch.");
                return;
            }

            if(pause == true)
            {
                pausedRows.Add(batch);
                return;
            }

            List<TexeraTuple> batchReceived = orderingEnforcer.PreProcess(batch.Value, this);
            List<TexeraTuple> batchToForward = new List<TexeraTuple>();
            if(batchReceived != null)
            {
                foreach(TexeraTuple tuple in batchReceived)
                {
                    TexeraTuple ret = await Process_impl(tuple);
                    if(ret != null)
                    {
                        batchToForward.Add(ret);
                    }
                }
            }
            
            var streamProvider = GetStreamProvider("SMSProvider");
            var stream = streamProvider.GetStream<Immutable<List<TexeraTuple>>>(this.GetPrimaryKey(out extensionKey), "Random");

            await orderingEnforcer.PostProcess(batchToForward, this, stream);
        }

        public override async Task<TexeraTuple> Process_impl(TexeraTuple tuple)
        {
            if(tuple.id != -1 && tuple.unit_cost < ((FilterPredicate)predicate).GetThreshold())
            {
                return null;
            }

            // bool cond = Program.conditions_on ? (row as Tuple).unit_cost > 50 : true;
            if (tuple.id == -1)
            {
                Console.WriteLine("Ordered Filter done");
                finished = true;
                return tuple;
            }

            return tuple;
        }
    }

}