﻿using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Orleans.Concurrency;
using TexeraUtilities;
using Engine.OperatorImplementation.Common;

namespace Engine.OperatorImplementation.MessagingSemantics
{
    public class OrderingGrainWithSequenceNumber : IOrderingEnforcer
    {
        private Dictionary<ulong, List<TexeraTuple>> stashed = new Dictionary<ulong, List<TexeraTuple>>();
        private ulong current_idx = 0;
        private ulong current_seq_num = 0;


        public ulong GetOutgoingSequenceNumber()
        {
            return current_seq_num;
        }

        public ulong GetExpectedSequenceNumber()
        {
            return current_idx;
        }

        public void IncrementOutgoingSequenceNumber()
        {
            current_seq_num++;
        }

        public void IncrementExpectedSequenceNumber()
        {
            current_idx++;
        }
        
        public List<TexeraTuple> PreProcess(List<TexeraTuple> batch, IProcessorGrain currentOperator)
        {
            var seq_token = batch[0].seq_token;
            string extensionKey = "";      

            if(seq_token < current_idx)
            {
                // de-dup messages
                Console.WriteLine($"Grain {currentOperator.GetPrimaryKey(out extensionKey)} received duplicate message with sequence number {seq_token}: expected sequence number {current_idx}");
                return null;
            }
            if (seq_token != current_idx)
            {
                Console.WriteLine($"Grain {currentOperator.GetPrimaryKey(out extensionKey)} received message ahead in sequence, being put in stash: sequence number {seq_token}, expected sequence number {current_idx}");                              
                stashed.Add(seq_token, batch);
                return null;           
            }
            else
            {
                current_idx++;
                return batch;
            }
        }

        public async Task PostProcess(List<TexeraTuple> batchToForward, IProcessorGrain currentOperator, IAsyncStream<Immutable<List<TexeraTuple>>> stream)
        {
            if (batchToForward.Count > 0)
            {
                INormalGrain nextGrain = await currentOperator.GetNextGrain();
                if (nextGrain != null)
                {
                    batchToForward[0].seq_token = current_seq_num;
                    current_seq_num++;
                    ((IProcessorGrain)nextGrain).Process(batchToForward.AsImmutable());
                }

            }
            await ProcessStashed(currentOperator);
        }       

        private async Task ProcessStashed(IProcessorGrain currentOperator)
        {
            while(stashed.ContainsKey(current_idx))
            {
                List<TexeraTuple> batch = stashed[current_idx];
                List<TexeraTuple> batchToForward = new List<TexeraTuple>();
                foreach(TexeraTuple tuple in batch)
                {
                    TexeraTuple ret = await currentOperator.Process_impl(tuple);
                    if(ret != null)
                    {
                        batchToForward.Add(ret);
                    }                
                }
                if (batchToForward.Count > 0)
                {
                    INormalGrain nextGrain = await currentOperator.GetNextGrain();
                    if(nextGrain != null)
                    {
                        batchToForward[0].seq_token = current_seq_num++;
                        ((IProcessorGrain)nextGrain).Process(batchToForward.AsImmutable());
                    }
                }
                stashed.Remove(current_idx);
                current_idx++;
            }

        }

    }
}
