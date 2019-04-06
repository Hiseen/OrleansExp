using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.Streams;
using Orleans.Concurrency;
using System.Diagnostics;
using TexeraUtilities;
using Engine;

namespace OrleansClient
{
    public class StreamObserver : IAsyncObserver<Immutable<PayloadMessage>>
    {
        public  List<TexeraTuple> resultsToRet = new List<TexeraTuple>();
        Stopwatch sw=new Stopwatch();
        public bool isFinished=false;

        public Task Start()
        {
            sw.Start();
            return Task.CompletedTask;
        }

        public Task OnCompletedAsync()
        {
            Console.WriteLine("Chatroom message stream received stream completed event");
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(Exception ex)
        {
            Console.WriteLine($"Chatroom is experiencing message delivery failure, ex :{ex}");
            return Task.CompletedTask;
        }

        public Task OnNextAsync(Immutable<PayloadMessage> item, StreamSequenceToken token = null)
        {
            if(item.Value.IsEnd)
            {
                isFinished=true;
                sw.Stop();
                Console.WriteLine("Time usage: " + sw.Elapsed);
            }
            else
            {
                List<TexeraTuple> results = item.Value.Payload;
                resultsToRet.AddRange(results);
                for(int i=0; i<results.Count; i++)
                {
                    Console.WriteLine($"=={results[i].CustomResult}, {results[i].TableID}, {results[i].FieldList}== count received: by client");
                }
            }
            return Task.CompletedTask;
        }
    }
}